// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Collections.Concurrent;
using AutoIME.Config;
using AutoIME.Detection;
using AutoIME.Diagnostics;
using AutoIME.IME;
using AutoIME.Input;
using AutoIME.Learning;

namespace AutoIME;

/// <summary>
/// 各モジュールをつなぐ (設計書 §4)。スレッドは 3 種類:
///   フックのスレッド   … KeyboardMonitor。InputSession に打鍵を渡すだけ。
///   再入力ワーカー     … IME 切替と保留分の再入力。待ちが発生する処理はすべてここ (STA)。
///   タイマー           … 無入力での出力、IME 状態のポーリング、学習データの保存。
/// </summary>
internal sealed class AutoImeEngine : ISessionEnvironment, IDisposable
{
    private const int ImeStateFreshMs = 1000;

    private readonly string? _configPath;
    private readonly UserModel _userModel;
    private readonly ScoreEngine _scoreEngine;
    private readonly InputSession _session;
    private readonly ForegroundTracker _foreground = new();
    private readonly Imm32ImeController _imm32 = new();
    private readonly TsfImeController _tsf = new();
    private readonly ImeController _ime;
    private readonly KeyInjector _injector = new();
    private readonly KeyboardMonitor _monitor;
    private readonly BlockingCollection<FlushRequest> _flushQueue = new();
    private readonly Thread _worker;
    private readonly System.Threading.Timer _sessionTimer;
    private readonly System.Threading.Timer _pollTimer;
    private readonly System.Threading.Timer _saveTimer;
    private volatile Settings _settings;
    private volatile ImeSnapshot? _imeSnapshot;
    private string? _lastDenyReason;
    private int _polling;
    private bool _disposed;

    private sealed record ImeSnapshot(IntPtr Window, ImeState State, long Time);

    public event Action? StatusChanged;

    /// <summary>Ctrl + 半角/全角 が押された (フックのスレッドから呼ばれる。受け取った側で UI スレッドに移して切り替える)。</summary>
    public event Action? ToggleRequested;

    /// <summary>判定の強さが「手動」で、IME 自動切替が日本語入力を提案した (ワーカースレッドから呼ばれる)。</summary>
    public event Action? ImeSuggested;

    private readonly HashSet<int> _toggleKeyDown = [];

    public AutoImeEngine(Settings settings, string? configPath, string? modelPath, string? userDictionaryDirectory)
    {
        _settings = settings.Clone().Normalize();
        _configPath = configPath;
        Log.SetFileOutput(_settings.FileLog ? AppPaths.LogFile : null);

        _userModel = new UserModel(modelPath);
        _scoreEngine = ScoreEngine.CreateDefault(_userModel, () => _settings, userDictionaryDirectory);
        _session = new InputSession(_scoreEngine, () => _settings, this);
        _ime = new ImeController(_imm32, _tsf, () => _settings);
        _monitor = new KeyboardMonitor(OnKey, OnMouseButton, OnForegroundChanged, OnFocusChanged);
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "AutoIME reinjection" };
        _worker.SetApartmentState(ApartmentState.STA);

        _sessionTimer = new System.Threading.Timer(_ => SafeRun(() => _session.OnTimer(Environment.TickCount64)), null, Timeout.Infinite, Timeout.Infinite);
        _pollTimer = new System.Threading.Timer(_ => PollImeState(), null, Timeout.Infinite, Timeout.Infinite);
        _saveTimer = new System.Threading.Timer(_ => SafeRun(_userModel.Save), null, Timeout.Infinite, Timeout.Infinite);
    }

    public Settings Settings => _settings;

    public UserModel UserModel => _userModel;

    public DetectionResult? LastDecision => _session.LastDecision;

    public void Start()
    {
        _worker.Start();
        _monitor.Start();
        _sessionTimer.Change(50, 50);
        _pollTimer.Change(0, 250);
        _saveTimer.Change(30_000, 30_000);
        Log.Info($"AutoIME を開始しました (モード: {_settings.Mode}, 有効: {(_settings.Enabled ? "ON" : "OFF")})。");
    }

    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            var next = _settings.Clone();
            next.Enabled = value;
            ApplySettings(next);
        }
    }

    // ---- AutoIME キーボード (変換ボックス) ----

    private Composition.CompositionService? _composition;
    private volatile bool _keyboardDirect;
    private volatile bool _directEnglishWord;
    private readonly HashSet<int> _swallowedToggleUps = [];

    /// <summary>UI スレッドで作った変換ボックスをつなぐ。</summary>
    public void AttachComposition(Composition.CompositionService composition)
    {
        _composition = composition;
        composition.Focus.Invalidate();
        if (IsKeyboardActive) CloseSystemImeAsync();
    }

    /// <summary>終了時。未確定の内容を確定してから切り離す (UI スレッドで呼ぶ)。</summary>
    public void DetachComposition()
    {
        var composition = _composition;
        _composition = null;
        composition?.Flush();
    }

    /// <summary>AutoIME キーボードが入力を受け付ける状態か (半角/全角 で直接入力にしていない)。</summary>
    public bool IsKeyboardActive => _settings.Enabled && _settings.Mode == InputMode.Keyboard && !_keyboardDirect;

    public bool KeyboardDirect
    {
        get => _keyboardDirect;
        set
        {
            _keyboardDirect = value;
            _directEnglishWord = false;
            Log.Info(value ? "AutoIME キーボード: 直接入力 (英数)" : "AutoIME キーボード: 日本語入力");
            if (!value) CloseSystemImeAsync();
            _composition?.ShowMode(!value);
            StatusChanged?.Invoke();
        }
    }

    private bool OnKey(KeyEvent e)
    {
        var settings = _settings;
        // Ctrl + 半角/全角: AutoIME 全体の有効/無効 (どちらのモードでも)。変換ボックスの後始末が要るので切り替え自体は UI スレッドで行う。
        if (VirtualKeys.IsHankakuZenkaku(e.Vk) && !e.Injected && (e.IsUp ? _toggleKeyDown.Remove(e.Vk) : IsDown(VirtualKeys.Control)))
        {
            if (e.IsDown)
            {
                _toggleKeyDown.Add(e.Vk);
                ToggleRequested?.Invoke();
            }
            return true;
        }
        if (settings.Mode != InputMode.Keyboard) return _session.OnKey(e);
        // 変換ボックスの準備前・終了処理中は何もしない (素通し)。
        if (_composition is not { } composition) return false;

        // 半角/全角 キーは Microsoft IME ではなく AutoIME キーボードの ON/OFF に使う。
        if (settings.Enabled && settings.HankakuTogglesKeyboard && VirtualKeys.IsHankakuZenkaku(e.Vk) && !e.Injected && !composition.Gate.IsCaptured)
        {
            if (e.IsDown)
            {
                lock (_swallowedToggleUps) _swallowedToggleUps.Add(e.Vk);
                KeyboardDirect = !_keyboardDirect;
                return true;
            }
            lock (_swallowedToggleUps) if (_swallowedToggleUps.Remove(e.Vk)) return true;
        }
        // 英数状態で英語と判定した単語は、区切りのキー (Space・記号など) が来たら終わり。次の単語はまた判定する。
        if (_keyboardDirect && e.IsDown && !VirtualKeys.IsLetter(e.Vk) && !VirtualKeys.IsModifier(e.Vk)) _directEnglishWord = false;
        var swallowed = composition.Gate.OnKey(e, StartsComposition);
        // AutoIME を通らずにアプリへ届いたキーはキャレットを動かすかもしれない。直前の語を確定し直さないようにする。
        if (!swallowed && e.IsDown && !VirtualKeys.IsModifier(e.Vk)) composition.ForgetLastCommit();
        return swallowed;
    }

    /// <summary>フックのスレッドで呼ばれる。この打鍵で変換ボックスを開くか。</summary>
    private bool StartsComposition(KeyEvent e)
    {
        if (!e.IsDown || e.Injected) return false;
        var settings = _settings;
        if (!settings.Enabled || settings.Mode != InputMode.Keyboard) return false;
        var letter = VirtualKeys.IsLetter(e.Vk);
        // 句読点・かぎかっこ・長音・中黒・数字 (、。「」ー・0-9) でも変換ボックスを開く (Shift なしのときだけ)。
        var punctuation = e.Vk is VirtualKeys.OemComma or VirtualKeys.OemPeriod or VirtualKeys.OemMinus or VirtualKeys.Oem2 or VirtualKeys.Oem4 or VirtualKeys.Oem6
            || e.Vk is >= 0x30 and <= 0x39;
        // Shift を押して打つ ！ ？ ～ (Shift+1 / Shift+/ / Shift+^ (JIS) / Shift+` (US))。
        var shiftedSymbol = e.Vk is 0x31 or VirtualKeys.Oem2 or 0xDE or 0xC0;
        var shift = IsDown(VirtualKeys.Shift);
        // かな入力 (JIS): かなのキー (数字・記号のキーも含む) はすべて、Shift を押していても入力を始める。
        if (settings.InputStyle == InputStyle.Kana && !_keyboardDirect && KanaDetector.IsKanaKey(e.Vk)) punctuation = shiftedSymbol = true;
        if (_keyboardDirect)
        {
            // 英数状態: ローマ字かどうかを判定するために、単語の打ち始めの英字だけを受け取る。
            // 英語と分かった単語の続きは、区切り (Space など) まで素通しする。
            if (!letter || !settings.DirectModeAutoDetect || settings.DetectionLevel == DetectionLevel.Manual || _directEnglishWord) return false;
        }
        else if (!letter && !punctuation && !(shift && shiftedSymbol))
        {
            return false;
        }
        if (IsDown(VirtualKeys.Control) || IsDown(VirtualKeys.Menu) || IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin)) return false;
        if (punctuation && shift && !shiftedSymbol) return false;
        if (!_foreground.Check(settings).Allowed) return false;
        // 文字入力欄 (パスワード以外) にフォーカスがあるときだけ。ショートカットキーやゲームの操作を横取りしない。
        return _composition?.Focus.CanCapture == true;
    }

    /// <summary>英数状態で打ち始めた英字がローマ字 (日本語) かを、IME 自動切替と同じ判定器で調べる。UI スレッドから呼ばれる。</summary>
    public Verdict ClassifyDirect(string letters, bool final)
    {
        var settings = _settings.Clone();
        // かな入力なら打鍵をかな配列として、それ以外はローマ字として判定する。
        settings.InputStyle = settings.InputStyle == InputStyle.Kana ? InputStyle.Kana : InputStyle.Romaji;
        var keys = letters.Select(c => (int)char.ToUpperInvariant(c)).ToArray();
        var result = _scoreEngine.Evaluate(new DetectionInput(letters, keys, final), settings);
        if (result.Verdict == Verdict.Japanese) Log.Decision($"英数状態でローマ字を検知: {result.Describe()}");
        return result.Verdict;
    }

    /// <summary>英数状態での判定結果 (UI スレッドから呼ばれる)。</summary>
    public void OnDirectDecided(bool japanese)
    {
        if (japanese) KeyboardDirect = false;
        else _directEnglishWord = true;
    }

    private bool OnMouseButton(Composition.MouseButtonEvent e)
    {
        if (_settings.Mode == InputMode.Keyboard && _composition is { } composition)
        {
            if (composition.Gate.OnMouseButton(e)) return true;
            if (IsButtonDown(e.Message))
            {
                composition.Focus.Invalidate();
                composition.ResetContext();
            }
            return false;
        }
        if (IsButtonDown(e.Message)) _session.OnContextChanged(Environment.TickCount64);
        return false;
    }

    private static bool IsButtonDown(int message) =>
        message is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN or Native.WM_XBUTTONDOWN;

    /// <summary>AutoIME キーボード使用中は Microsoft IME を閉じておく (二重に変換されないように)。</summary>
    private void CloseSystemImeAsync()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (!IsKeyboardActive || ImeTarget.FromForeground() is not { } target) return;
                var state = _imm32.GetState(target);
                if (state.Mode == IME.ImeMode.Open && _imm32.TrySetOpen(target, false, null, _settings.ImeTimeoutMs))
                {
                    Log.Info("AutoIME キーボードを使うため Microsoft IME を OFF にしました。");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Microsoft IME を OFF にできませんでした: {ex.Message}");
            }
        });
    }

    public void ApplySettings(Settings settings)
    {
        var next = settings.Clone().Normalize();
        var previous = _settings;
        _settings = next;
        if (!next.Enabled || next.Mode != InputMode.Keyboard) _composition?.Flush();
        if (next.Mode == InputMode.Keyboard && (previous.Mode != InputMode.Keyboard || !previous.Enabled)) CloseSystemImeAsync();
        if (!next.Enabled) FlushAbandoned(_session.Abort());
        Log.SetFileOutput(next.FileLog ? AppPaths.LogFile : null);
        if (_configPath is not null)
        {
            try { next.Save(_configPath); }
            catch (Exception ex) { Log.Warn($"config.json を保存できませんでした: {ex.Message}"); }
        }
        Log.Info($"設定を反映しました (モード: {next.Mode}, 有効: {(next.Enabled ? "ON" : "OFF")}, 判定の強さ: {next.DetectionLevel}, 閾値: {next.EffectiveJapaneseThreshold}, 入力方式: {next.InputStyle})。");
        StatusChanged?.Invoke();
    }

    public DetectionResult Evaluate(string letters, Settings? settings = null) =>
        _scoreEngine.Evaluate(new DetectionInput(letters, letters.Select(c => (int)char.ToUpperInvariant(c)).ToArray(), IsFinal: false), settings ?? _settings);

    // ---- ISessionEnvironment (InputSession のロック内から呼ばれる。重い処理はしない) ----

    bool ISessionEnvironment.IsModifierDown() =>
        IsDown(VirtualKeys.Control) || IsDown(VirtualKeys.Menu) || IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin) || IsDown(VirtualKeys.Shift);

    CollectPermission ISessionEnvironment.CanCollect()
    {
        var permission = _foreground.Check(_settings);
        if (permission.Allowed)
        {
            // 既に日本語入力になっているなら保留する意味がない (遅延を出さない)。
            var snapshot = _imeSnapshot;
            if (snapshot is not null && snapshot.Window == _foreground.Current.Window &&
                Environment.TickCount64 - snapshot.Time < ImeStateFreshMs && snapshot.State.IsJapaneseReady)
            {
                permission = CollectPermission.Deny("既に日本語入力");
            }
        }
        if (!permission.Allowed && permission.Reason != _lastDenyReason && permission.Reason != "既に日本語入力")
        {
            Log.Info($"保留しません: {permission.Reason}");
        }
        _lastDenyReason = permission.Reason;
        return permission;
    }

    void ISessionEnvironment.RequestFlush(FlushRequest request)
    {
        Log.Decision(request.Result.Describe());
        if (!_flushQueue.IsAddingCompleted) _flushQueue.Add(request);
    }

    void ISessionEnvironment.SessionEnded(SessionSummary summary)
    {
        if (summary.UserCorrected)
        {
            Log.Decision($"誤判定のフィードバック: \"{summary.Letters}\" は {summary.Verdict} ではなかった → {summary.Outcome}");
        }
        if (_settings.LearningEnabled)
        {
            _userModel.Learn(summary.Letters, summary.Outcome, summary.Verdict == Verdict.English);
        }
    }

    // ---- 再入力ワーカー ----

    private void WorkerLoop()
    {
        foreach (var request in _flushQueue.GetConsumingEnumerable())
        {
            try
            {
                if (request.Result.Verdict == Verdict.Japanese && _settings.DetectionLevel == DetectionLevel.Manual)
                {
                    // 手動: 切り替えずに提案だけ出す。
                    Log.Decision($"日本語入力の提案 (手動のため切り替えない): {request.Result.Describe()}");
                    ImeSuggested?.Invoke();
                }
                else if (request.Result.Verdict == Verdict.Japanese)
                {
                    var target = ImeTarget.FromForeground();
                    var result = _ime.EnsureJapanese(target);
                    var message = $"IME 切替: {result.Outcome} ({result.Before} → {result.After}) {result.Detail}";
                    if (result.Success) Log.Decision(message);
                    else Log.Warn(message + " — 保留分は切り替えずにそのまま出力します。");
                    if (target is not null) _imeSnapshot = new ImeSnapshot(target.TopLevel, result.After, Environment.TickCount64);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"IME 切替で例外: {ex.Message}");
            }
            finally
            {
                // 何があっても保留分は必ず出力する (入力を失わない)。
                Drain();
            }
            StatusChanged?.Invoke();
        }
    }

    private void Drain()
    {
        try
        {
            while (_session.TakePendingForFlush() is { } events) _injector.Inject(events);
        }
        catch (Exception ex)
        {
            Log.Error($"再入力で例外: {ex.Message}");
        }
    }

    private void FlushAbandoned(List<KeyEvent> events)
    {
        if (events.Count > 0) _injector.Inject(events);
    }

    // ---- フック・タイマーからの通知 ----

    private void OnFocusChanged()
    {
        _composition?.Focus.Invalidate();
        _directEnglishWord = false;
        _composition?.ResetContext();
        _session.OnContextChanged(Environment.TickCount64);
    }

    private void OnForegroundChanged(IntPtr window)
    {
        _foreground.Refresh(window);
        _composition?.Focus.Invalidate();
        _directEnglishWord = false;
        _composition?.ResetContext();
        _session.OnContextChanged(Environment.TickCount64);
        if (IsKeyboardActive) CloseSystemImeAsync();
    }

    private void PollImeState()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try
        {
            if (!_settings.Enabled) return;
            var target = ImeTarget.FromForeground();
            if (target is null) return;
            var state = _imm32.GetState(target);
            _imeSnapshot = new ImeSnapshot(target.TopLevel, state, Environment.TickCount64);
            if (_settings.Mode == InputMode.Keyboard) KeepSystemImeClosed(target, state);
        }
        catch (Exception ex)
        {
            Log.Warn($"IME 状態の取得に失敗: {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
    }

    private IntPtr _lastLayout;
    private long _lastForcedClose;
    private int _forcedCloses;

    /// <summary>
    /// AutoIME キーボードを使っている間は、Windows の IME (Microsoft IME・Google 日本語入力など) を OFF に保つ。
    /// Win+Space などで IME を切り替えると、新しい IME が ON の状態で始まることがあり、そのままだと
    /// AutoIME が通したキーを IME が変換してしまう (AutoIME の変換ボックスが出たり IME の変換が出たりする)。
    /// </summary>
    private void KeepSystemImeClosed(ImeTarget target, ImeState state)
    {
        var layout = Native.GetKeyboardLayout(target.ThreadId);
        if (layout != _lastLayout)
        {
            if (_lastLayout != IntPtr.Zero)
            {
                Log.Info($"キーボード/IME が切り替わりました (HKL {_lastLayout:X} → {layout:X}, IME: {state})。");
                _composition?.Focus.Invalidate();
            }
            _lastLayout = layout;
        }
        if (state.Mode != IME.ImeMode.Open) return;
        var composition = _composition;
        // 変換ボックスで入力中は、確定の直前に閉じる (CompositionService) ので触らない。
        if (composition is null || composition.Gate.IsCaptured) return;
        var now = Environment.TickCount64;
        // ユーザーが IME を ON にし続ける (閉じてもすぐ開く) なら、10 秒に 3 回までにして奪い合わない。
        if (now - _lastForcedClose > 10000) _forcedCloses = 0;
        if (_forcedCloses >= 3) return;
        _forcedCloses++;
        _lastForcedClose = now;
        var closed = _imm32.TrySetOpen(target, false, null, _settings.ImeTimeoutMs);
        Log.Warn($"AutoIME キーボードの使用中に Windows の IME が ON になっていました ({state}) → {(closed ? "OFF にしました" : "OFF にできませんでした")}。" +
                 (_forcedCloses == 3 ? " (続けて ON になるため、しばらく自動で OFF にしません。IME を使うときは Ctrl+半角/全角 で AutoIME を一時停止してください)" : ""));
    }

    public ImeState? CurrentImeState => _imeSnapshot?.State;

    public void ResetLearning()
    {
        _userModel.Reset();
        Log.Info("学習データをリセットしました。");
    }

    private static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static void SafeRun(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Error(ex.Message); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.Dispose();
        _sessionTimer.Dispose();
        _pollTimer.Dispose();
        _saveTimer.Dispose();
        _flushQueue.CompleteAdding();
        if (_worker.IsAlive) _worker.Join(3000); // Start 前 (起動に失敗したとき) でも Dispose できるように
        FlushAbandoned(_session.Abort());
        _userModel.Save();
        Log.Info("AutoIME を終了しました。");
        Log.FlushFile();
    }
}
