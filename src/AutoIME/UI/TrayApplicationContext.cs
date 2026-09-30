using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using AutoIME.Config;

namespace AutoIME.UI;

/// <summary>タスクトレイ (設計書 §28)。Ctrl+Alt+F12 で自動切替の一時停止/再開。</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AutoImeEngine _engine;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly HotkeyWindow _hotkey;
    private readonly Control _invoker = new();
    private readonly Icon _onIcon = CreateIcon("あ", Color.FromArgb(0, 120, 212));
    private readonly Icon _directIcon = CreateIcon("A", Color.FromArgb(0, 120, 212));
    private readonly Icon _offIcon = CreateIcon("A", Color.FromArgb(120, 120, 120));
    private readonly ToolStripMenuItem _keyboardModeItem;
    private readonly ToolStripMenuItem _autoSwitchModeItem;
    private readonly ToolStripMenuItem _levelItem;
    private readonly Composition.CompositionService _composition;
    private SettingsForm? _settingsForm;
    private LogForm? _logForm;
    private UserDictionaryForm? _dictionaryForm;

    public TrayApplicationContext(AutoImeEngine engine)
    {
        _engine = engine;
        _invoker.CreateControl();
        var detector = Composition.CompositionDetector.CreateDefault(AppPaths.UserDictionaryDirectory);
        detector.SpellChecker = Detection.WindowsSpellChecker.Shared;
        _composition = new Composition.CompositionService(_invoker, detector, new Composition.CompositionOptions
        {
            LiveConversion = () => _engine.Settings.LiveConversion,
            DirectMode = () => _engine.KeyboardDirect,
            ClassifyDirect = _engine.ClassifyDirect,
            DirectDecided = _engine.OnDirectDecided,
            AutoCorrect = () => _engine.Settings.AutoCorrectAfterCommit && _engine.Settings.DetectionLevel != DetectionLevel.Manual,
            Level = () => _engine.Settings.DetectionLevel,
            KanaInput = () => _engine.Settings.InputStyle == InputStyle.Kana,
            ModeIndicator = () => _engine.Settings is { Enabled: true, Mode: InputMode.Keyboard, ShowModeIndicator: true },
        });
        _engine.AttachComposition(_composition);

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem { Enabled = false };
        _enabledItem = new ToolStripMenuItem("AutoIME を有効にする", null, (_, _) => ToggleEnabled()) { CheckOnClick = false };
        _keyboardModeItem = new ToolStripMenuItem("AutoIME キーボード (変換ボックスで入力)", null, (_, _) => SetMode(InputMode.Keyboard));
        _autoSwitchModeItem = new ToolStripMenuItem("IME 自動切替 (Microsoft IME を使う)", null, (_, _) => SetMode(InputMode.AutoSwitch));
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_keyboardModeItem);
        menu.Items.Add(_autoSwitchModeItem);
        // 自動判定の強さ (積極的 / 標準 / 慎重 / 手動)
        _levelItem = new ToolStripMenuItem("自動判定の強さ");
        foreach (var level in Enum.GetValues<DetectionLevel>())
        {
            _levelItem.DropDownItems.Add(new ToolStripMenuItem(LevelName(level), null, (_, _) => SetLevel(level)) { Tag = level });
        }
        menu.Items.Add(_levelItem);
        menu.Items.Add("設定...", null, (_, _) => ShowSettings());
        menu.Items.Add("ユーザー辞書...", null, (_, _) => ShowUserDictionary());
        menu.Items.Add("ログ / 判定理由...", null, (_, _) => ShowLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("データフォルダを開く", null, (_, _) => OpenDataFolder());
        menu.Items.Add("学習データをリセット", null, (_, _) => ResetLearning());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => UpdateStatus();

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ToggleEnabled();
        _hotkey = new HotkeyWindow(ToggleEnabled);
        _engine.ToggleRequested += OnToggleRequested;
        _enabledItem.Text = _hotkey.Name is { } hotkey ? $"AutoIME を有効にする (Ctrl+半角/全角, {hotkey})" : "AutoIME を有効にする (Ctrl+半角/全角)";
        _engine.StatusChanged += OnEngineStatusChanged;
        _engine.ImeSuggested += OnImeSuggested;
        UpdateStatus();
    }

    private void OnToggleRequested()
    {
        if (_invoker.IsHandleCreated && !_invoker.IsDisposed) _invoker.BeginInvoke(ToggleEnabled);
    }

    private long _lastSuggestion = long.MinValue;

    private void OnImeSuggested()
    {
        if (!_invoker.IsHandleCreated || _invoker.IsDisposed) return;
        _invoker.BeginInvoke(() =>
        {
            // 打つたびに出るとうるさいので 30 秒に 1 回まで。
            var now = Environment.TickCount64;
            if (now - _lastSuggestion < 30000) return;
            _lastSuggestion = now;
            _tray.ShowBalloonTip(2000, "AutoIME (手動)", "日本語を打っているようです。半角/全角 で日本語入力にできます。", ToolTipIcon.Info);
        });
    }

    private void OnEngineStatusChanged()
    {
        if (_invoker.IsHandleCreated && !_invoker.IsDisposed) _invoker.BeginInvoke(UpdateStatus);
    }

    private void ToggleEnabled()
    {
        _engine.Enabled = !_engine.Enabled;
        _tray.ShowBalloonTip(1500, "AutoIME", _engine.Enabled ? "AutoIME を有効にしました" : "AutoIME を一時停止しました", ToolTipIcon.Info);
    }

    private static string LevelName(DetectionLevel level) => level switch
    {
        DetectionLevel.Aggressive => "積極的 (英語らしければすぐ英字)",
        DetectionLevel.Balanced => "標準",
        DetectionLevel.Conservative => "慎重 (確信度が高いときだけ英字)",
        _ => "手動 (提案のみ・Tab で英字)",
    };

    private void SetLevel(DetectionLevel level)
    {
        var next = _engine.Settings.Clone();
        next.DetectionLevel = level;
        _engine.ApplySettings(next);
        UpdateStatus();
    }

    private void SetMode(InputMode mode)
    {
        var next = _engine.Settings.Clone();
        next.Mode = mode;
        next.Enabled = true;
        _engine.ApplySettings(next);
    }

    private void UpdateStatus()
    {
        var settings = _engine.Settings;
        var enabled = settings.Enabled;
        var keyboard = settings.Mode == InputMode.Keyboard;
        _enabledItem.Checked = enabled;
        _keyboardModeItem.Checked = keyboard;
        _autoSwitchModeItem.Checked = !keyboard;
        foreach (ToolStripMenuItem item in _levelItem.DropDownItems) item.Checked = item.Tag is DetectionLevel level && level == settings.DetectionLevel;
        _levelItem.Text = $"自動判定の強さ: {LevelName(settings.DetectionLevel).Split(' ')[0]}";

        string status;
        if (!enabled)
        {
            _tray.Icon = _offIcon;
            status = "一時停止中";
        }
        else if (keyboard)
        {
            _tray.Icon = _engine.KeyboardDirect ? _directIcon : _onIcon;
            status = _engine.KeyboardDirect ? "キーボード: 直接入力 (半角/全角 で日本語)" : "キーボード: 日本語";
        }
        else
        {
            _tray.Icon = _onIcon;
            var last = _engine.LastDecision;
            status = last is null || last.Text.Length == 0 ? "IME 自動切替: ON" : $"IME 自動切替: ON / 直前の判定: {last.Verdict}";
        }
        _statusItem.Text = status;
        var tip = $"AutoIME — {status}";
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_engine);
        _settingsForm.FormClosed += (_, _) => UpdateStatus();
        _settingsForm.Show();
    }

    private void ShowUserDictionary()
    {
        if (_dictionaryForm is { IsDisposed: false })
        {
            _dictionaryForm.Activate();
            return;
        }
        _dictionaryForm = new UserDictionaryForm(_composition);
        _dictionaryForm.Show();
    }

    private void ShowLog()
    {
        if (_logForm is { IsDisposed: false })
        {
            _logForm.Activate();
            return;
        }
        _logForm = new LogForm(_engine);
        _logForm.Show();
    }

    private static void OpenDataFolder()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"") { UseShellExecute = true });
    }

    private void ResetLearning()
    {
        var answer = MessageBox.Show("学習データ (model.json と、選び直した変換の記録 conversions.json) をすべて削除します。よろしいですか？", "AutoIME", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.OK)
        {
            _engine.ResetLearning();
            _composition.History.Clear();
        }
    }

    protected override void ExitThreadCore()
    {
        _engine.StatusChanged -= OnEngineStatusChanged;
        _engine.ToggleRequested -= OnToggleRequested;
        _engine.ImeSuggested -= OnImeSuggested;
        _engine.DetachComposition();
        _composition.Dispose();
        _settingsForm?.Close();
        _logForm?.Close();
        _dictionaryForm?.Close();
        _hotkey.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _invoker.Dispose();
        base.ExitThreadCore();
    }

    private static Icon CreateIcon(string text, Color background)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(background);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Yu Gothic UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, Brushes.White, (32 - size.Width) / 2, (32 - size.Height) / 2 + 1);
        }
        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312, Id = 1;
        private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000, VK_F12 = 0x7B;
        private readonly Action _pressed;

        // 他のアプリと衝突したら順に次の候補を試す。
        private static readonly (uint Modifiers, uint Key, string Name)[] Candidates =
        [
            (MOD_CONTROL | MOD_ALT, VK_F12, "Ctrl+Alt+F12"),
            (MOD_CONTROL | MOD_ALT, 0x7A, "Ctrl+Alt+F11"),
            (MOD_CONTROL | MOD_ALT | MOD_SHIFT, 0x41, "Ctrl+Shift+Alt+A"),
            (MOD_CONTROL | MOD_ALT, 0x13, "Ctrl+Alt+Pause"),
        ];

        public string? Name { get; }

        public HotkeyWindow(Action pressed)
        {
            _pressed = pressed;
            CreateHandle(new CreateParams { Caption = "AutoIME Hotkey" });
            foreach (var (modifiers, key, name) in Candidates)
            {
                if (!RegisterHotKey(Handle, Id, modifiers | MOD_NOREPEAT, key)) continue;
                Name = name;
                Diagnostics.Log.Info($"一時停止/再開のホットキー: {name}");
                return;
            }
            Diagnostics.Log.Warn("一時停止/再開のホットキーを登録できませんでした (候補がすべて他のアプリで使用中)。トレイアイコンのダブルクリックで切り替えてください。");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == Id) _pressed();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterHotKey(Handle, Id);
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
