// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meltype.Config;

public enum InputStyle
{
    /// <summary>ローマ字入力。</summary>
    [Description("ローマ字入力")] Romaji,
    /// <summary>JIS かな入力。</summary>
    [Description("かな入力 (JIS)")] Kana,
    /// <summary>両方を判定する。</summary>
    [Description("両方を判定")] Both,
}

/// <summary>英語か日本語かの自動判定の強さ。</summary>
public enum DetectionLevel
{
    /// <summary>少しでも英語らしければ英字にする。</summary>
    [Description("積極的 (Aggressive)")] Aggressive,
    /// <summary>既定。短い語 (no, to) は前後が英語のときだけ英字。</summary>
    [Description("標準 (Balanced)")] Balanced,
    /// <summary>確信度が高いときだけ英字にする。</summary>
    [Description("慎重 (Conservative)")] Conservative,
    /// <summary>自動では切り替えず、提案だけ出す。</summary>
    [Description("手動 (提案のみ)")] Manual,
}

/// <summary>かな漢字変換のエンジン。</summary>
public enum ConversionEngine
{
    /// <summary>Mozc で変換し、使えないときは OS の変換エンジン。候補は両方。</summary>
    [Description("両方 (Mozc を優先)")] Hybrid,
    [Description("Mozc")] Mozc,
    /// <summary>OS の変換エンジン (Windows では Microsoft IME)。</summary>
    [Description("Microsoft IME")] System,
}

public enum InputMode
{
    /// <summary>Meltype 自身の変換ボックスで入力する (半角/全角 不要)。</summary>
    [Description("Meltype キーボード (変換ボックスで入力)")] Keyboard,
    /// <summary>入力開始時に判定して Microsoft IME の ON/OFF を切り替える (v1 の動作)。</summary>
    [Description("IME 自動切替 (Microsoft IME を使う)")] AutoSwitch,
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class AppRule
{
    [DisplayName("プロセス名"), Description("例: code.exe / chrome.exe / WindowsTerminal.exe")]
    public string Process { get; set; } = "";

    [DisplayName("自動切替"), Description("false にするとこのアプリでは一切キーを保留しません。")]
    public bool Enabled { get; set; } = true;

    [DisplayName("種類"), Description("コード = コードエディターやターミナル。基本は英数で、コメントや \"…\" の中だけ日本語を判定します。")]
    public AppProfile Profile { get; set; } = AppProfile.General;

    /// <summary>ユーザーが作った種類 (<see cref="Settings.AppKinds"/>) の名前。指定があれば Profile より優先。</summary>
    public string? Kind { get; set; }

    public override string ToString() => $"{Process}: {(Enabled ? "ON" : "OFF")}, {Kind ?? Profile.ToString()}";
}

/// <summary>
/// ユーザーが作るアプリの種類 (例: 「チャット」「ゲーム」)。一般 / コード を元に、判定の強さ・ライブ変換・最初の入力モードを変えられる。
/// null の項目は全体の設定のまま。
/// </summary>
public sealed class AppKind
{
    public string Name { get; set; } = "";
    public AppProfile Base { get; set; } = AppProfile.General;
    public DetectionLevel? DetectionLevel { get; set; }
    public bool? LiveConversion { get; set; }
    /// <summary>このアプリに切り替えたら英数 (直接入力) から始める。</summary>
    public bool StartInEnglish { get; set; }

    public AppKind Clone() => (AppKind)MemberwiseClone();
}

/// <summary>アプリの種類。アプリに合わせて、英語と日本語のどちらを基本にするかを変える。</summary>
public enum AppProfile
{
    /// <summary>一般 (文章を書くアプリ)。日本語が基本で、英単語を自動で見分ける。</summary>
    [Description("一般")] General,
    /// <summary>コードエディター・ターミナル。英数が基本で、コメントと文字列 ("…") の中だけ日本語を判定する。</summary>
    [Description("コード")] Code,
}

/// <summary>
/// config.json の内容。値はすべて保守的な初期値にしてある (設計書 §18)。
/// インスタンスは不変として扱い、変更時は Clone して差し替える。
/// </summary>
public sealed class Settings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Category("1. 全般"), DisplayName("Meltype を有効にする")]
    public bool Enabled { get; set; } = true;

    [Category("1. 全般"), DisplayName("動作モード"),
     Description("Keyboard = Meltype の変換ボックスで入力 (英単語は自動で英字、Space で変換、Enter で確定) / AutoSwitch = 入力開始時に判定して Microsoft IME を自動で ON にする")]
    public InputMode Mode { get; set; } = InputMode.Keyboard;

    [Category("1. 全般"), DisplayName("半角/全角 で Meltype を ON/OFF"),
     Description("Keyboard モードで、変換ボックスが出ていないときの 半角/全角 キーを Meltype キーボードの ON/OFF (直接入力) に使います。")]
    public bool HankakuTogglesKeyboard { get; set; } = true;

    [Category("1. 全般"), DisplayName("確定後も文脈に合わせて直す"),
     Description("英語とも日本語とも読める語 (i, sushi など) を確定した後、次の語で英語か日本語かがはっきりしたら自動で確定し直します (i → 胃 と確定した後に want と打つと I want)。")]
    public bool AutoCorrectAfterCommit { get; set; } = true;

    [Category("1. 全般"), DisplayName("英数状態でもローマ字を検知"),
     Description("Keyboard モードの英数 (直接入力) 状態でも単語の打ち始めを判定し、ローマ字 (日本語) なら自動で日本語入力に戻します。")]
    public bool DirectModeAutoDetect { get; set; } = true;

    [Category("1. 全般"), DisplayName("ライブ変換"),
     Description("Keyboard モードで、Space を押さなくても打ったそばから漢字に変換して表示します。")]
    public bool LiveConversion { get; set; } = true;

    [Category("1. 全般"), DisplayName("変換エンジン"),
     Description("かな漢字変換に使うエンジン。「両方」は Mozc (Google 日本語入力のオープンソース版) で変換し、Mozc が使えないときは Microsoft IME で変換します。候補には両方の候補が出ます。")]
    public ConversionEngine ConversionEngine { get; set; } = ConversionEngine.Hybrid;

    [Category("1. 全般"), DisplayName("英訳の候補"),
     Description("変換の候補の後ろに英訳も出します (複雑な → complex, complicated)。JMdict のよく使う語から。選んだ英訳は少しずつ前に出ます。")]
    public bool TranslationCandidates { get; set; } = true;

    [Category("1. 全般"), DisplayName("入力モードをカーソルの近くに表示"),
     Description("入力欄をクリックしたときと 半角/全角 を押したときに、カーソルの近くに「あ」(日本語) か「A」(英数) を一瞬表示します。Meltype キーボードの使用中は Windows の IME を OFF にしているので、タスクバーの IME の表示は常に「A」になります。今のモードはこの表示かトレイの Meltype のアイコンで確認してください。")]
    public bool ShowModeIndicator { get; set; } = true;

    [Category("1. 全般"), DisplayName("入力方式"), Description("ローマ字入力 / かな入力 (JIS) / 両方を判定。Meltype キーボードでは、かな入力を選ぶと JIS かな配列で入力し (Shift+E = ぃ, Shift+Z = っ, Shift+ね = 、)、打ったキーの英字が英単語なら英字で見せます。「両方を判定」は IME 自動切替のみ (Meltype キーボードではローマ字入力)。")]
    public InputStyle InputStyle { get; set; } = InputStyle.Romaji;

    [Category("2. 判定"), DisplayName("自動判定の強さ"),
     Description("積極的 = 英語らしければすぐ英字 / 標準 = 短い語 (no, to, ga) は前後が英語のときだけ英字 / 慎重 = 確信度が高いときだけ英字 / 手動 = 自動では切り替えず提案だけ (変換ボックスで Tab を押すと提案どおり英字に)。Meltype キーボード・IME 自動切替・英数状態の検知・かな入力のすべてに効きます。")]
    public DetectionLevel DetectionLevel { get; set; } = DetectionLevel.Balanced;

    /// <summary>判定の強さを反映した日本語判定の閾値 (IME 自動切替・英数状態の検知)。</summary>
    [Browsable(false), JsonIgnore]
    public int EffectiveJapaneseThreshold => JapaneseThreshold + DetectionLevel switch
    {
        DetectionLevel.Aggressive => -1,
        DetectionLevel.Conservative => 3,
        _ => 0,
    };

    [Category("2. 判定"), DisplayName("日本語判定の閾値"), Description("JapaneseScore がこの値以上、かつ EnglishScore をこの値以上上回ったときだけ切り替えます。大きいほど誤爆が減ります。")]
    public int JapaneseThreshold { get; set; } = 4;

    [Category("2. 判定"), DisplayName("Typo 判定を使う"), Description("辞書語との編集距離 1 以内を補助的な日本語スコアとして加点します。Typo 一致だけでは切り替えません。")]
    public bool TypoEnabled { get; set; } = true;

    [Category("3. 保留"), DisplayName("最大保留キー数"), Description("判定のために保留する文字数の上限。超えたら判定不能としてそのまま出力します。")]
    public int MaxPendingKeys { get; set; } = 6;

    [Category("3. 保留"), DisplayName("無入力で出力するまでの時間 (ms)")]
    public int IdleFlushMs { get; set; } = 700;

    [Category("3. 保留"), DisplayName("最大保留時間 (ms)"), Description("最初のキーからこの時間が経ったら、判定途中でも保留分を出力します。")]
    public int MaxHoldMs { get; set; } = 2500;

    /// <summary>config.json の形式のバージョン。古い既定値を持つ設定ファイルを移行するのに使う。</summary>
    [Browsable(false)]
    public int SettingsVersion { get; set; } = CurrentVersion;

    public const int CurrentVersion = 4;

    [Category("4. セッション"), DisplayName("新しいセッションとみなす無入力時間 (ms)")]
    public int SessionIdleMs { get; set; } = 1500;

    [Category("4. セッション"), DisplayName("英語の後の Space では判定しない"), Description("英語と判定した直後に Space で区切られた次の単語は、保留せずそのまま通します (英文入力中の遅延を減らします)。")]
    public bool ContinueEnglishAfterSpace { get; set; }

    [Category("5. 学習"), DisplayName("ユーザー学習を使う")]
    public bool LearningEnabled { get; set; } = true;

    [Category("5. 学習"), DisplayName("誤判定フィードバックの受付時間 (ms)"), Description("自動判定の後、この時間内に 半角/全角 などの IME 切替キーが押されたら誤判定として学習します。")]
    public int FeedbackWindowMs { get; set; } = 4000;

    [Category("6. IME"), DisplayName("TSF を使う"), Description("IMM32 で入力言語を切り替えられなかったとき、TSF のプロファイル切替を試します。")]
    public bool UseTsf { get; set; } = true;

    [Category("6. IME"), DisplayName("IME 操作のタイムアウト (ms)")]
    public int ImeTimeoutMs { get; set; } = 300;

    [Category("7. アプリ"), DisplayName("全画面アプリでは無効"), Description("ゲームや動画など全画面のウィンドウではキーを保留しません。")]
    public bool ExcludeFullscreen { get; set; } = true;

    [Category("7. アプリ"), DisplayName("アプリ別設定"), Description("プロセス名ごとに、自動切替の ON/OFF と種類を指定します。種類「コード」(コードエディター・ターミナル) では基本は英数のままで、コメント (// # -- など) と文字列 (\"…\" など) の中だけ日本語を判定します。コードの行で 半角/全角 を押すと、その行だけ日本語で入力できます。README.md などの文章ファイルを開いているときは一般として扱います。")]
    public List<AppRule> AppRules { get; set; } = DefaultAppRules();

    [Category("7. アプリ"), DisplayName("独自の種類"), Description("アプリ別設定の「種類」に使える、自分で作る種類です。一般 / コード を元に、判定の強さ・ライブ変換・最初は英数にするか を変えられます (「全体と同じ」なら上の設定のまま)。")]
    public List<AppKind> AppKinds { get; set; } = [];

    [Category("8. ログ"), DisplayName("ファイルにログを書く"), Description("%LOCALAPPDATA%\\Meltype\\meltype.log に判定ログを書きます。判定対象の先頭数文字が含まれます。")]
    public bool FileLog { get; set; }

    public static List<AppRule> DefaultAppRules() =>
    [
        // キー入力が別のマシン/VM に届くアプリ。保留すると相手側の IME 状態と食い違う。
        new() { Process = "mstsc.exe", Enabled = false },
        new() { Process = "msrdc.exe", Enabled = false },
        new() { Process = "vmconnect.exe", Enabled = false },
        new() { Process = "VirtualBoxVM.exe", Enabled = false },
        new() { Process = "vmware-vmx.exe", Enabled = false },
        new() { Process = "vmware.exe", Enabled = false },
        .. CodeApps.Select(process => new AppRule { Process = process, Enabled = true, Profile = AppProfile.Code }),
    ];

    /// <summary>既定で「コード」として扱うアプリ (コードエディター・IDE・ターミナル)。</summary>
    public static readonly string[] CodeApps =
    [
        "Code.exe", "Code - Insiders.exe", "Cursor.exe", "Windsurf.exe", "zed.exe", "devenv.exe",
        "idea64.exe", "pycharm64.exe", "webstorm64.exe", "rider64.exe", "clion64.exe", "goland64.exe",
        "phpstorm64.exe", "rubymine64.exe", "datagrip64.exe", "studio64.exe", "sublime_text.exe", "notepad++.exe",
        "WindowsTerminal.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "wezterm-gui.exe", "alacritty.exe", "mintty.exe",
    ];

    public bool IsAppEnabled(string? processName) => FindRule(processName)?.Enabled ?? true;

    /// <summary>アプリの種類 (アプリ別設定に無ければ一般)。独自の種類なら、その元にした種類。</summary>
    public AppProfile ProfileFor(string? processName) =>
        KindFor(processName)?.Base ?? FindRule(processName)?.Profile ?? AppProfile.General;

    /// <summary>アプリに割り当てた独自の種類 (無ければ null)。</summary>
    public AppKind? KindFor(string? processName) =>
        FindRule(processName)?.Kind is { Length: > 0 } name ? AppKinds.FirstOrDefault(k => k.Name == name) : null;

    /// <summary>アプリの独自の種類で変えた項目 (判定の強さ・ライブ変換) を反映した設定。変えていなければ自分自身。</summary>
    public Settings ForApp(string? processName)
    {
        if (KindFor(processName) is not { } kind || (kind.DetectionLevel is null && kind.LiveConversion is null)) return this;
        var copy = Clone();
        if (kind.DetectionLevel is { } level) copy.DetectionLevel = level;
        if (kind.LiveConversion is { } live) copy.LiveConversion = live;
        return copy;
    }

    private AppRule? FindRule(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return null;
        foreach (var rule in AppRules)
        {
            if (string.Equals(rule.Process, processName, StringComparison.OrdinalIgnoreCase)) return rule;
        }
        return null;
    }

    public Settings Clone()
    {
        var copy = (Settings)MemberwiseClone();
        copy.AppRules = AppRules.Select(r => new AppRule { Process = r.Process, Enabled = r.Enabled, Profile = r.Profile, Kind = r.Kind }).ToList();
        copy.AppKinds = AppKinds.Select(k => k.Clone()).ToList();
        return copy;
    }

    /// <summary>手で編集された config.json の極端な値を安全な範囲に収める。</summary>
    public Settings Normalize()
    {
        JapaneseThreshold = Math.Clamp(JapaneseThreshold, 2, 20);
        MaxPendingKeys = Math.Clamp(MaxPendingKeys, 2, 12);
        IdleFlushMs = Math.Clamp(IdleFlushMs, 100, 3000);
        MaxHoldMs = Math.Clamp(MaxHoldMs, 200, 5000);
        SessionIdleMs = Math.Clamp(SessionIdleMs, 300, 60000);
        FeedbackWindowMs = Math.Clamp(FeedbackWindowMs, 500, 30000);
        ImeTimeoutMs = Math.Clamp(ImeTimeoutMs, 50, 2000);
        AppRules ??= [];
        AppKinds ??= [];
        AppRules.RemoveAll(r => r is null || string.IsNullOrWhiteSpace(r.Process));
        foreach (var rule in AppRules) rule.Process = rule.Process.Trim();
        return this;
    }

    /// <summary>古い既定値のままの項目だけを新しい既定値に更新する (ユーザーが変えた値は触らない)。</summary>
    internal bool Migrate()
    {
        if (SettingsVersion >= CurrentVersion) return false;
        if (SettingsVersion < 2)
        {
            // v1 の 400ms / 1200ms では、ゆっくり打つと判定前に保留が切れて日本語を見逃していた。
            if (IdleFlushMs == 400) IdleFlushMs = 700;
            if (MaxHoldMs == 1200) MaxHoldMs = 2500;
        }
        // v3: Meltype キーボード (変換ボックス) を追加。v2 以前の config.json には Mode が無いので、
        // 読み込み時に初期値 (Keyboard) になる。
        if (SettingsVersion < 4)
        {
            // v4: アプリの種類 (コード) を追加。コードエディター・ターミナルを「コード」にする (ユーザーが OFF にしたものはそのまま)。
            foreach (var process in CodeApps)
            {
                var rule = FindRule(process);
                if (rule is null) AppRules.Add(new AppRule { Process = process, Enabled = true, Profile = AppProfile.Code });
                else rule.Profile = AppProfile.Code;
            }
        }
        SettingsVersion = CurrentVersion;
        return true;
    }

    public static Settings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new Settings();
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
            if (!json.Contains(nameof(SettingsVersion))) settings.SettingsVersion = 1;
            if (settings.Migrate()) settings.Save(path);
            return settings.Normalize();
        }
        catch (Exception ex)
        {
            // 壊れた設定で起動不能にしない。元ファイルは退避して既定値で動く。
            try { File.Copy(path, path + ".broken", overwrite: true); } catch { }
            Diagnostics.Log.Warn($"config.json を読み込めなかったため既定値を使います: {ex.Message}");
            return new Settings();
        }
    }

    /// <summary>config.json と同じ形式の文字列 (変更があったかを比べるのに使う)。</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
