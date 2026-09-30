using System.Runtime.InteropServices;
using AutoIME.Input;

namespace AutoIME.Composition;

/// <summary>
/// AutoIME キーボードを Windows につなぐ部分 (UI スレッドで作る)。
///   フック → CaptureGate → (BeginInvoke) → CompositionController → 変換ボックス表示 / SendInput で確定
/// </summary>
internal sealed class CompositionService : ICompositionHost, IDisposable
{
    private readonly Control _invoker;
    private readonly CompositionWindow _window = new();
    private readonly ModeIndicatorWindow _indicator = new();
    private readonly Func<bool> _showIndicator;
    private readonly Func<bool> _directMode;
    private readonly MsImeKanjiConverter _converter = new();
    private readonly KeyInjector _injector = new();
    private readonly WinRtCandidates _windowsCandidates = new();
    private readonly IME.Imm32ImeController _imm32 = new();
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 100 };
    private int _pumpScheduled;

    /// <param name="options">LiveConversion などの設定。Candidates を指定しなければ組み込みの補助辞書を読む。</param>
    public CompositionService(Control invoker, CompositionDetector detector, CompositionOptions options)
    {
        _invoker = invoker;
        Gate = new CaptureGate(SchedulePump);
        // 補助辞書・文脈の手がかり・学習データは、指定がなければ既定の場所から読む。
        var userDirectory = Config.AppPaths.UserDictionaryDirectory;
        History = options.History ?? new ConversionHistory(Config.AppPaths.ConversionHistoryFile);
        UserDictionary = options.UserDictionary ?? new UserDictionary(Config.AppPaths.UserDictionaryFile);
        _detector = detector;
        var resolved = new CompositionOptions
        {
            LiveConversion = options.LiveConversion,
            DirectMode = options.DirectMode,
            ClassifyDirect = options.ClassifyDirect,
            DirectDecided = options.DirectDecided,
            Candidates = options.Candidates ?? CandidateDictionary.Load(userDirectory),
            ContextRules = options.ContextRules ?? ContextRules.Load(userDirectory),
            History = History,
            UserDictionary = UserDictionary,
            MoreCandidates = options.MoreCandidates ?? (reading => _windowsCandidates.Get(reading)),
            AutoCorrect = options.AutoCorrect,
            Level = options.Level,
            KanaInput = options.KanaInput,
        };
        Controller = new CompositionController(Gate, detector, _converter, this, resolved);
        _showIndicator = options.ModeIndicator;
        _directMode = options.DirectMode;
        Focus.TextInputEntered += () => ShowMode(!_directMode());
        Controller.Committed += text => Diagnostics.Log.Decision($"確定: 「{(text.Length > 20 ? text[..20] + "…" : text)}」");
        _tick.Tick += (_, _) => Safely(() => Controller.Tick(Environment.TickCount64));
        _tick.Start();
    }

    public CaptureGate Gate { get; }

    public CompositionController Controller { get; }

    /// <summary>選び直した変換の学習データ (トレイの「学習データをリセット」で消す)。</summary>
    public ConversionHistory History { get; }

    /// <summary>ユーザー辞書 (トレイの「ユーザー辞書...」で編集する)。</summary>
    public UserDictionary UserDictionary { get; }

    private readonly CompositionDetector _detector;

    /// <summary>
    /// ユーザー辞書の登録画面用: 入力された読み (ローマ字でもよい) をひらがなにする。
    /// </summary>
    public string ToReading(string input)
    {
        var text = input.Trim();
        return text.Any(char.IsAsciiLetter) ? _detector.Romaji.ConvertLenient(text.ToLowerInvariant(), final: true) : text;
    }

    /// <summary>ユーザー辞書の登録画面用: 読みの変換候補 (変換エンジンの結果 → Windows の候補一覧 → カタカナ)。</summary>
    public IReadOnlyList<string> SuggestWords(string reading)
    {
        var list = new List<string>();
        void Add(string? word) { if (!string.IsNullOrEmpty(word) && !list.Contains(word)) list.Add(word); }
        Add(_converter.Convert(reading));
        foreach (var word in _windowsCandidates.Get(reading, 30)) Add(word);
        Add(CompositionText.ToKatakana(reading));
        return list;
    }

    public FocusInspector Focus { get; } = new();

    public bool ConverterAvailable => _converter.IsAvailable;

    private void SchedulePump()
    {
        if (!_invoker.IsHandleCreated || _invoker.IsDisposed) return;
        if (Interlocked.Exchange(ref _pumpScheduled, 1) == 1) return;
        _invoker.BeginInvoke(() =>
        {
            Volatile.Write(ref _pumpScheduled, 0);
            Safely(Controller.Pump);
        });
    }

    /// <summary>
    /// 変換ボックスの処理で例外が起きたら、未確定の内容と状態を捨てて、残りの入力はそのまま通す。
    /// 状態を捨てないと、次の打鍵でも同じ例外が起きて英字しか入力できなくなる。
    /// </summary>
    private void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Error($"変換ボックスで例外が起きたので入力をリセットしました: {ex}");
            try { Controller.Reset(); } catch { }
            ReplayAll(Gate.Abort());
            Hide();
        }
    }

    /// <summary>フォーカス変更・クリックでキャレットが動いたとき (どのスレッドからでも呼べる)。直前の英語/日本語の文脈を忘れる。</summary>
    /// <summary>AutoIME を通らずにキーが押された (キャレットが動いたかもしれない) とき (どのスレッドからでも呼べる)。</summary>
    public void ForgetLastCommit()
    {
        if (_invoker.IsHandleCreated && !_invoker.IsDisposed) _invoker.BeginInvoke(Controller.ForgetLastCommit);
    }

    public void ResetContext()
    {
        if (_invoker.IsHandleCreated && !_invoker.IsDisposed) _invoker.BeginInvoke(Controller.ResetContext);
    }

    /// <summary>自動切替を止めたときなど。未確定の内容を確定し、残りの入力を通す。</summary>
    public void Flush()
    {
        Controller.CommitPending();
        ReplayAll(Gate.Abort());
        Hide();
    }

    private void ReplayAll(List<CapturedInput> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Key is { } key) Replay(key);
            else if (input.Mouse is { } mouse) Replay(mouse);
        }
    }

    // ---- ICompositionHost ----

    public void CommitText(string text)
    {
        EnsureSystemImeClosed();
        var inputs = new List<Native.INPUT>(text.Length * 2);
        foreach (var c in text)
        {
            inputs.Add(UnicodeInput(c, up: false));
            inputs.Add(UnicodeInput(c, up: true));
        }
        var array = inputs.ToArray();
        var sent = Native.SendInput((uint)array.Length, array, Marshal.SizeOf<Native.INPUT>());
        if (sent != array.Length) Diagnostics.Log.Error($"確定文字列の入力に失敗しました ({sent}/{array.Length})。");
    }

    /// <summary>
    /// 確定文字列を送る前に、アプリ側の Microsoft IME が閉じていることを確かめる。
    /// IME が開いたままだと、送り込んだ文字の一部を IME が抱え込み、後から来た文字と順番が入れ替わることがある
    /// (「やあやあ、私だよ」→「やあやあ、だよ私」)。
    /// </summary>
    private void EnsureSystemImeClosed()
    {
        try
        {
            if (IME.ImeTarget.FromForeground() is not { } target) return;
            var state = _imm32.GetState(target);
            if (state.Mode != IME.ImeMode.Open) return;
            var closed = _imm32.TrySetOpen(target, false, null, 150);
            Diagnostics.Log.Warn($"確定時に Microsoft IME が ON になっていました ({state}) → {(closed ? "OFF にしてから入力します" : "OFF にできませんでした")}。");
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Warn($"Microsoft IME の状態を確認できませんでした: {ex.Message}");
        }
    }

    public void Replay(KeyEvent e) => _injector.Inject([e]);

    public void DeleteBackward(int count)
    {
        if (count <= 0) return;
        var events = new List<KeyEvent>(count * 2);
        for (var i = 0; i < count; i++)
        {
            events.Add(new KeyEvent(VirtualKeys.Back, 0, false, false, false, 0));
            events.Add(new KeyEvent(VirtualKeys.Back, 0, false, true, false, 0));
        }
        _injector.Inject(events);
    }

    public void RequestSurroundingText(Action<string?, string?> callback)
    {
        Focus.RequestSurroundingText((before, after) =>
        {
            if (_invoker.IsHandleCreated && !_invoker.IsDisposed) _invoker.BeginInvoke(() => Safely(() => callback(before, after)));
        });
    }

    public void Replay(MouseButtonEvent e)
    {
        var (button, data) = e.Message switch
        {
            Native.WM_LBUTTONDOWN => (Native.MOUSEEVENTF_LEFTDOWN, 0u),
            Native.WM_LBUTTONUP => (Native.MOUSEEVENTF_LEFTUP, 0u),
            Native.WM_RBUTTONDOWN => (Native.MOUSEEVENTF_RIGHTDOWN, 0u),
            Native.WM_RBUTTONUP => (Native.MOUSEEVENTF_RIGHTUP, 0u),
            Native.WM_MBUTTONDOWN => (Native.MOUSEEVENTF_MIDDLEDOWN, 0u),
            Native.WM_MBUTTONUP => (Native.MOUSEEVENTF_MIDDLEUP, 0u),
            Native.WM_XBUTTONDOWN => (Native.MOUSEEVENTF_XDOWN, e.MouseData >> 16),
            Native.WM_XBUTTONUP => (Native.MOUSEEVENTF_XUP, e.MouseData >> 16),
            _ => (0u, 0u),
        };
        if (button == 0) return;
        // 元のクリック位置で再生する (保留中にカーソルが動いていても同じ場所をクリックする)。
        var left = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
        var top = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
        var width = Math.Max(1, Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN) - 1);
        var height = Math.Max(1, Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN) - 1);
        var input = new Native.INPUT
        {
            type = Native.INPUT_MOUSE,
            u = new Native.InputUnion
            {
                mi = new Native.MOUSEINPUT
                {
                    dx = (int)Math.Round((e.X - left) * 65535.0 / width),
                    dy = (int)Math.Round((e.Y - top) * 65535.0 / height),
                    mouseData = data,
                    dwFlags = button | Native.MOUSEEVENTF_MOVE | Native.MOUSEEVENTF_ABSOLUTE | Native.MOUSEEVENTF_VIRTUALDESK,
                    dwExtraInfo = KeyboardMonitor.InjectedMarker,
                },
            },
        };
        Native.SendInput(1, [input], Marshal.SizeOf<Native.INPUT>());
    }

    public char? CharFromKey(KeyEvent e, bool shift)
    {
        shift |= (Native.GetAsyncKeyState(VirtualKeys.Shift) & 0x8000) != 0;
        var state = new byte[256];
        if (shift) state[VirtualKeys.Shift] = state[VirtualKeys.LShift] = 0x80;
        var foreground = Native.GetForegroundWindow();
        var layout = Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(foreground, out _));
        var buffer = new char[8];
        // flags 0x4: キーボードの状態 (デッドキー) を変更しない (Windows 10 1607 以降)。
        var count = Native.ToUnicodeEx((uint)e.Vk, (uint)e.Scan, state, buffer, buffer.Length, 0x4, layout);
        return count == 1 && !char.IsControl(buffer[0]) ? buffer[0] : null;
    }

    /// <summary>
    /// 入力モード (日本語なら「あ」、英数なら「A」) をカーソルの近くに一瞬出す。どのスレッドから呼んでもよい。
    /// 変換ボックスが出ているときは出さない。
    /// </summary>
    public void ShowMode(bool japanese)
    {
        if (!_invoker.IsHandleCreated || _invoker.IsDisposed) return;
        _invoker.BeginInvoke(() => Safely(() =>
        {
            if (!_showIndicator() || _window.Visible) return;
            var anchor = FindAnchor();
            _indicator.Flash(japanese, new Point(anchor.X + 2, anchor.Y + 2));
        }));
    }

    public bool IsShiftDown() => (Native.GetAsyncKeyState(VirtualKeys.Shift) & 0x8000) != 0;

    public void Show(CompositionView view)
    {
        _window.ShowView(view, _window.Visible ? null : FindAnchor());
    }

    public void Hide()
    {
        if (_window.Visible) _window.Hide();
    }

    /// <summary>変換ボックスを出す位置。キャレット → 入力欄の左下 → マウスカーソルの順に試す。</summary>
    private Point FindAnchor()
    {
        var foreground = Native.GetForegroundWindow();
        var thread = Native.GetWindowThreadProcessId(foreground, out _);
        var info = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        if (Native.GetGUIThreadInfo(thread, ref info) && info.hwndCaret != IntPtr.Zero)
        {
            var point = new Native.POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };
            if (Native.ClientToScreen(info.hwndCaret, ref point) && (point.X != 0 || point.Y != 0))
            {
                return new Point(point.X, point.Y + 4);
            }
        }
        if (Focus.Current.Bounds is { } bounds && bounds.Height is > 0 and < 120)
        {
            return new Point(bounds.Left, bounds.Bottom + 2);
        }
        Native.GetCursorPos(out var cursor);
        return new Point(cursor.X + 12, cursor.Y + 20);
    }

    private static Native.INPUT UnicodeInput(char c, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT
            {
                wVk = 0,
                wScan = c,
                dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0),
                dwExtraInfo = KeyboardMonitor.InjectedMarker,
            },
        },
    };

    public void Dispose()
    {
        _tick.Dispose();
        Focus.Dispose();
        _converter.Dispose();
        _windowsCandidates.Dispose();
        _window.Dispose();
        _indicator.Dispose();
    }
}
