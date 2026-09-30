// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Composition;
using Meltype.Input;

namespace Meltype.Tests;

/// <summary>Meltype キーボード (変換ボックス)。</summary>
internal static class CompositionTests
{
    internal static readonly CompositionDetector Detector = CompositionDetector.CreateDefault();

    internal sealed class FakeConverter : IKanjiConverter
    {
        public string? Convert(string hiragana) => hiragana switch
        {
            "きょう" => "今日",
            "にほんご" => "日本語",
            "こんにちは" => "今日は",
            "きょうは" => "今日は",
            "でけんさく" => "で検索",
            "たんい" => "単位",
            _ => null,
        };

        /// <summary>直近の ConvertClauses に渡された文脈 (文脈なしの呼び出しは数えない)。</summary>
        public string? LastContext { get; private set; }

        public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null)
        {
            if (context is not null) LastContext = context;
            return hiragana switch
            {
                "たんいをとる" => [new("たんいを", "単位を"), new("とる", "取る")],
                // 本物の変換エンジンは、前に同じ語があると区切りを変える (「記号等」含め、 + きごうとう → 気強盗)。
                "きごうとう" when context?.Contains("記号等") == true => [new("き", "気"), new("ごうとう", "強盗")],
                "きごうとう" => [new("きごう", "記号"), new("とう", "等")],
                "あつい" => [new("あつい", "熱い")],
                "かわ" => [new("かわ", "川")],
                "はしを" => [new("はしを", "橋を")],
                _ => null,
            };
        }
    }

    internal sealed class FakeHost : ICompositionHost
    {
        public List<string> Output { get; } = [];
        public List<string> Events { get; } = [];
        public CompositionView? View { get; private set; }
        public bool PhysicalShift { get; set; }

        /// <summary>入力欄のキャレットの直前にある (と見なす) 確定済みの文字列。</summary>
        public string? PrecedingText { get; set; }

        /// <summary>入力欄のキャレットの後ろにある (と見なす) 文字列。</summary>
        public string? FollowingText { get; set; }

        public void RequestSurroundingText(Action<string?, string?> callback) => callback(PrecedingText ?? (Document.Length > 0 ? Document : null), FollowingText);

        /// <summary>入力欄の中身 (確定した文字列と、確定し直すときの削除を反映したもの)。</summary>
        public string Document { get; private set; } = "";

        public void DeleteBackward(int count)
        {
            Events.Add($"bs:{count}");
            Document = Document[..Math.Max(0, Document.Length - count)];
        }

        public void CommitText(string text)
        {
            Output.Add(text);
            Events.Add($"text:{text}");
            Document += text;
        }

        public void Replay(KeyEvent e) => Events.Add($"{(e.IsUp ? "up" : "down")}:{e.Vk:X2}");
        public void Replay(MouseButtonEvent e) => Events.Add($"mouse:{e.Message:X}");

        public char? CharFromKey(KeyEvent e, bool shift)
        {
            if (VirtualKeys.IsLetter(e.Vk)) return shift || PhysicalShift ? (char)e.Vk : char.ToLowerInvariant((char)e.Vk);
            var shifted = shift || PhysicalShift;
            foreach (var (c, key) in JisKeys)
            {
                if (key.Vk == e.Vk && key.Shift == shifted) return c;
            }
            return null;
        }

        /// <summary>JIS 配列の数字・記号のキー (文字 → 仮想キー, Shift)。</summary>
        public static readonly Dictionary<char, (int Vk, bool Shift)> JisKeys = new()
        {
            ['0'] = (0x30, false), ['1'] = (0x31, false), ['2'] = (0x32, false), ['3'] = (0x33, false), ['4'] = (0x34, false),
            ['5'] = (0x35, false), ['6'] = (0x36, false), ['7'] = (0x37, false), ['8'] = (0x38, false), ['9'] = (0x39, false),
            ['!'] = (0x31, true), ['"'] = (0x32, true), ['#'] = (0x33, true), ['$'] = (0x34, true), ['%'] = (0x35, true),
            ['&'] = (0x36, true), ['\''] = (0x37, true), ['('] = (0x38, true), [')'] = (0x39, true),
            ['-'] = (0xBD, false), ['='] = (0xBD, true), ['^'] = (0xDE, false), ['~'] = (0xDE, true), ['\\'] = (0xDC, false), ['|'] = (0xDC, true),
            ['@'] = (0xC0, false), ['`'] = (0xC0, true), ['['] = (0xDB, false), ['{'] = (0xDB, true),
            [';'] = (0xBB, false), ['+'] = (0xBB, true), [':'] = (0xBA, false), ['*'] = (0xBA, true), [']'] = (0xDD, false), ['}'] = (0xDD, true),
            [','] = (0xBC, false), ['<'] = (0xBC, true), ['.'] = (0xBE, false), ['>'] = (0xBE, true), ['/'] = (0xBF, false), ['?'] = (0xBF, true),
            ['_'] = (0xE2, true),
        };

        public bool IsShiftDown() => PhysicalShift;

        public void Show(CompositionView view) => View = view;
        public void Hide() => View = null;
    }

    internal sealed class Keyboard
    {
        private long _now = 1000;
        private bool _ctrlHeld;
        public CaptureGate Gate { get; } = new(() => { });
        public FakeHost Host { get; } = new();
        public CompositionController Controller { get; }

        private static readonly Detection.ScoreEngine DirectEngine = TestSupport.CreateEngine();
        private static readonly CandidateDictionary Candidates = CandidateDictionary.Load(null);
        private static readonly ContextRules Rules = ContextRules.Load(null);
        private static readonly MisspellingDictionary Misspellings = MisspellingDictionary.Load(null);
        private bool _directEnglishWord;

        /// <summary>自動判定の強さ。</summary>
        public Meltype.Config.DetectionLevel Level { get; set; } = Meltype.Config.DetectionLevel.Balanced;

        private bool _shiftHeld;

        /// <summary>かな入力 (JIS) か。</summary>
        public bool Kana { get; set; }

        /// <summary>かな入力で、仮想キーを順に打つ (shift: その打鍵で Shift を押す)。</summary>
        public void TypeKeys(params (int Vk, bool Shift)[] keys)
        {
            foreach (var (vk, shift) in keys)
            {
                if (shift)
                {
                    Host.PhysicalShift = true;
                    Key(VirtualKeys.LShift);
                }
                Press(vk);
                if (shift)
                {
                    Key(VirtualKeys.LShift, up: true);
                    Host.PhysicalShift = false;
                }
            }
        }

        /// <summary>かな入力で、英字キー (と , . / - の記号キー) を打つ。大文字は Shift を押して打つ。</summary>
        public void TypeKanaKeys(string keys) =>
            TypeKeys(keys.Select(c => (c switch { ',' => 0xBC, '.' => 0xBE, '/' => 0xBF, '-' => 0xBD, '@' => 0xC0, '[' => 0xDB, _ => (int)char.ToUpperInvariant(c) }, char.IsAsciiLetterUpper(c))).ToArray());

        /// <summary>英数 (直接入力) 状態か。</summary>
        public bool Direct { get; set; }

        public long Now => _now;

        public FakeConverter Converter { get; } = new();

        public Keyboard(bool live = false, bool direct = false, ConversionHistory? history = null, IKanjiConverter? converter = null,
            Func<string, IReadOnlyList<string>>? moreCandidates = null, UserDictionary? userDictionary = null, LanguageMemory? languages = null)
        {
            Direct = direct;
            Controller = new CompositionController(Gate, Detector, converter ?? Converter, Host, new CompositionOptions
            {
                LiveConversion = () => live,
                DirectMode = () => Direct,
                ClassifyDirect = (letters, final) => DirectEngine.Evaluate(new Detection.DetectionInput(letters, letters.Select(c => (int)char.ToUpperInvariant(c)).ToArray(), final)).Verdict,
                DirectDecided = japanese => { if (japanese) Direct = false; else _directEnglishWord = true; },
                Candidates = Candidates,
                ContextRules = Rules,
                History = history ?? new ConversionHistory(null),
                MoreCandidates = moreCandidates,
                UserDictionary = userDictionary,
                Level = () => Level,
                KanaInput = () => Kana,
                Misspellings = Misspellings,
                Languages = languages,
            });
        }

        /// <summary>MeltypeEngine.StartsComposition と同じ条件。</summary>
        private bool Starts(KeyEvent k)
        {
            if (!k.IsDown || _ctrlHeld) return false;
            var letter = VirtualKeys.IsLetter(k.Vk);
            if (Direct) return letter && !_directEnglishWord && Level != Meltype.Config.DetectionLevel.Manual;
            if (Kana && Detection.KanaDetector.IsKanaKey(k.Vk)) return true;
            return letter || k.Vk is >= 0x30 and <= 0x39 or >= 0xBA and <= 0xC0 or >= 0xDB and <= 0xDF or 0xE2;
        }

        /// <summary>フックと同じく、関所が閉じていれば英字キーで変換ボックスを開く。</summary>
        public bool Key(int vk, bool up = false)
        {
            var e = new KeyEvent(vk, 0, false, up, false, _now += 30);
            // 実際のフックと同じく、Ctrl が押されている間は変換ボックスを開かない。
            if (vk == VirtualKeys.LControl) _ctrlHeld = !up;
            if (vk == VirtualKeys.LShift) _shiftHeld = !up;
            if (Direct && !up && !VirtualKeys.IsLetter(vk) && !VirtualKeys.IsModifier(vk)) _directEnglishWord = false;
            var swallowed = Gate.OnKey(e, Starts);
            if (!swallowed) Host.Events.Add($"{(up ? "passed-up" : "passed")}:{vk:X2}");
            Controller.Pump();
            return swallowed;
        }

        public void Press(int vk)
        {
            Key(vk);
            Key(vk, up: true);
        }

        public void Type(string text)
        {
            foreach (var c in text)
            {
                if (char.IsAsciiLetterUpper(c))
                {
                    Host.PhysicalShift = !Gate.IsCaptured;
                    Key(VirtualKeys.LShift);
                    Press(c);
                    Key(VirtualKeys.LShift, up: true);
                    Host.PhysicalShift = false;
                }
                else if (c == ' ') Press(VirtualKeys.Space);
                else if (c == '\n') Press(VirtualKeys.Return);
                else if (c == '\b') Press(VirtualKeys.Back);
                else if (FakeHost.JisKeys.TryGetValue(c, out var key) && key.Shift)
                {
                    Host.PhysicalShift = !Gate.IsCaptured;
                    Key(VirtualKeys.LShift);
                    Press(key.Vk);
                    Key(VirtualKeys.LShift, up: true);
                    Host.PhysicalShift = false;
                }
                else if (FakeHost.JisKeys.TryGetValue(c, out key)) Press(key.Vk);
                else Press(char.ToUpperInvariant(c));
            }
        }

        public string? Showing => Host.View?.Text;
    }

    [Test]
    public static void Romaji_IsShownAsKanaAndCommittedWithEnter()
    {
        var k = new Keyboard();
        k.Type("konnnichiha");
        Assert.Equal("こんにちは", k.Showing, "未確定の間は変換ボックスにかなで表示");
        Assert.Equal(0, k.Host.Output.Count, "Enter まではテキストボックスに何も入らない");
        k.Type("\n");
        Assert.Equal("こんにちは", k.Host.Output.Single());
        Assert.True(k.Showing is null, "確定したら変換ボックスを閉じる");
        Assert.True(!k.Gate.IsCaptured, "確定後はキーを横取りしない");
    }

    [Test]
    public static void Google_IsShownAsEnglish_NotGoogle_Kana()
    {
        var k = new Keyboard();
        k.Type("goog");
        Assert.Equal("goog", k.Showing, "固有名詞 (Google) の先頭と分かった時点で英字");
        k.Type("le");
        Assert.Equal("google", k.Showing, "英単語と分かった時点で英字に切り替わる (ごおｇぇ にならない)");
        k.Type("\n");
        Assert.Equal("google", k.Host.Output.Single());
    }

    [Test]
    public static void OnlyTheEnglishWordBecomesEnglish()
    {
        // 前に日本語があっても、英単語の部分だけが英字になる。
        var cases = new Dictionary<string, string>
        {
            ["kyouhagoogle"] = "きょうはgoogle",
            ["googlede"] = "googleで",
            ["kyouhagoogledekensaku"] = "きょうはgoogleでけんさく",
            ["githubnipush"] = "githubにpush",
            ["repo"] = "れぽ", // ローマ字としても読める語は日本語のまま (F10 で英字)
            ["koreha"] = "これは",
        };
        foreach (var (typed, expected) in cases)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(expected, k.Showing, $"「{typed}」");
        }
    }

    // 実機で報告: 「やあやあ、私だよ」→「やあやあ、だよ私」、「ところでgoogleって」→「ところでってgoogle」
    [Test]
    public static void ReportedOrderSwaps()
    {
        foreach (var live in new[] { false, true })
        {
            var k = new Keyboard(live);
            k.Type("tokorodegooglette\n");
            Assert.Equal("ところでgoogleって", string.Concat(k.Host.Output), $"live={live}");

            k = new Keyboard(live);
            k.Type("yaayaa,watashidayo\n");
            Assert.Equal("やあやあ、わたしだよ", string.Concat(k.Host.Output), $"live={live}");
        }
    }

    [Test]
    public static void Clauses_SelectWithArrowsAndConvertEach()
    {
        var k = new Keyboard();
        k.Type("tanniwotoru ");
        Assert.Equal("単位を|取る", string.Join("|", k.Host.View!.Clauses!), "Space で文節に区切って変換");
        Assert.Equal(0, k.Host.View.SelectedClause, "最初は先頭の文節を選択");
        k.Press(VirtualKeys.Right);
        Assert.Equal(1, k.Host.View.SelectedClause, "→ で次の文節");
        k.Type(" ");
        Assert.Equal("単位を|とる", string.Join("|", k.Host.View.Clauses!), "Space で選択中の文節だけ次の候補");
        k.Press(VirtualKeys.Left);
        Assert.Equal(0, k.Host.View.SelectedClause, "← で前の文節");
        k.Type("\n");
        Assert.Equal("単位をとる", k.Host.Output.Single());
    }

    [Test]
    public static void Arrows_BeforeSpace_EnterClauseSelection()
    {
        // 報告: 矢印キーで文節を選ぼうとすると確定してしまう → 変換前でも矢印で文節の選択に入る。
        var k = new Keyboard();
        k.Type("tanniwotoru");
        k.Press(VirtualKeys.Left);
        Assert.True(k.Host.View!.Converting, "← で文節の選択に入る (確定しない)");
        Assert.Equal(0, k.Host.Output.Count);
        Assert.Equal(1, k.Host.View.SelectedClause, "← なら最後の文節から");
        k.Press(VirtualKeys.Left);
        Assert.Equal(0, k.Host.View.SelectedClause);
        k.Type(" ");
        k.Type("\n");
        Assert.Equal("たんいを取る", k.Host.Output.Single(), "選んだ文節だけ候補が変わる");
    }

    [Test]
    public static void Candidates_IncludeHomophonesFromDictionary()
    {
        // 報告: とうてん が 当店 しか出ない。
        var k = new Keyboard();
        k.Type("toutenn ");
        Assert.True(k.Host.View!.Candidates.Contains("読点"), string.Join(",", k.Host.View.Candidates));
        k = new Keyboard();
        k.Type("hashiwo ");
        Assert.True(k.Host.View!.Candidates.Contains("箸を") && k.Host.View.Candidates.Contains("端を"), "助詞付きの文節でも同音異義語を出す: " + string.Join(",", k.Host.View.Candidates));
    }

    [Test]
    public static void NAndSmallKanaSpellings()
    {
        var cases = new Dictionary<string, string>
        {
            ["kaxnji"] = "かんじ", ["kannji"] = "かんじ", ["kanji"] = "かんじ", // ん: n / xn / nn
            ["who"] = "うぉ", ["ulo"] = "うぉ", ["uxo"] = "うぉ",
            ["xtu"] = "っ", ["ltsu"] = "っ", ["vu"] = "ゔ", ["thi"] = "てぃ",
        };
        foreach (var (typed, expected) in cases)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(expected, k.Showing, $"「{typed}」");
        }
    }

    [Test]
    public static void Context_FromTextBeforeCaret()
    {
        // 直前に確定済みの文字 (入力欄から読む) が英語なら英語、日本語なら日本語。
        var k = new Keyboard();
        k.Host.PrecedingText = "I love ";
        k.Type("sushi");
        Assert.Equal("sushi", k.Showing);

        k = new Keyboard();
        k.Host.PrecedingText = "今日は";
        k.Type("sushi");
        Assert.Equal("すし", k.Showing);

        k = new Keyboard();
        k.Host.PrecedingText = "hello";
        k.Type(",");
        Assert.Equal(",", k.Showing, "英文の続きのカンマは半角");

        k = new Keyboard();
        k.Host.PrecedingText = "今日は";
        k.Type(",");
        Assert.Equal("、", k.Showing, "日本語の続きなら読点");
    }

    [Test]
    public static void Comma_AtEnd_DoesNotBreakFollowingInput()
    {
        // 報告: 読点を最後に打つと英数に固定される。
        var k = new Keyboard();
        k.Type("kyouha,\n");
        k.Type("konnnichiha\n");
        Assert.Equal("きょうは、|こんにちは", string.Join("|", k.Host.Output));
    }

    [Test]
    public static void DirectMode_RomajiSwitchesBackToJapanese()
    {
        var k = new Keyboard(direct: true);
        k.Type("konnnichiha");
        Assert.True(!k.Direct, "ローマ字だと分かったら日本語入力に戻る");
        Assert.Equal("こんにちは", k.Showing, "保留していた英字も変換ボックスに入る");
        Assert.Equal(0, k.Host.Output.Count);
        Assert.True(!k.Host.Events.Any(e => e.StartsWith("down:")), "英字をアプリに送ってはいない");
    }

    [Test]
    public static void DirectMode_EnglishPassesThroughInOrder()
    {
        var k = new Keyboard(direct: true);
        k.Type("hello world");
        Assert.True(k.Direct, "英語なら英数のまま");
        Assert.True(k.Showing is null, "変換ボックスは出さない");
        Assert.Equal("h,e,l,l,o, ,w,o,r,l,d", Letters(k.Host.Events), "保留した分も素通しした分も、打った順番どおりに届く");
    }

    private static string Letters(List<string> events)
    {
        // 再生した押下 (down) と素通しした押下 (passed) を順に並べる。キーアップは数えない。
        return string.Join(",", events
            .Select(e => e.Split(':'))
            .Where(p => p[0] is "down" or "passed")
            .Select(p => System.Convert.ToInt32(p[1], 16))
            .Select(vk => vk == 0x20 ? " " : char.ToLowerInvariant((char)vk).ToString()));
    }

    [Test]
    public static void DirectMode_IdleReleasesHeldKeys()
    {
        var k = new Keyboard(direct: true);
        k.Type("ka");
        Assert.Equal(0, k.Host.Events.Count(e => e.StartsWith("down:")), "判定できるまでは保留");
        k.Controller.Tick(k.Now + 1000);
        Assert.Equal(2, k.Host.Events.Count(e => e.StartsWith("down:")), "しばらく打たなければ英語として出す");
        Assert.True(!k.Gate.IsCaptured, "保留をやめたら横取りもやめる");
    }

    [Test]
    public static void ProperNouns_AreEnglishEvenIfRomajiReadable()
    {
        var cases = new Dictionary<string, string>
        {
            ["amazon"] = "amazon", ["adobe"] = "adobe", ["netflix"] = "netflix", ["spotify"] = "spotify",
            ["kyouhaamazondekaimono"] = "きょうはamazonでかいもの",
            ["nihongonobenkyou"] = "にほんごのべんきょう", // 短い名前 (Ben) は文の途中で英語にしない
            ["suzuki"] = "すずき", // 日本語で書くことが多い名前は固有名詞辞書に入れていない
        };
        foreach (var (typed, expected) in cases)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(expected, k.Showing, $"「{typed}」");
        }
    }

    [Test]
    public static void ProperNouns_OfferCanonicalCasing()
    {
        var k = new Keyboard();
        k.Type("iphonede");
        k.Press(VirtualKeys.Left); // 最後の文節 (で)
        k.Press(VirtualKeys.Left); // iphone
        k.Type(" ");
        Assert.Equal("iPhone", k.Host.View!.Clauses![0], "候補に正しい大文字小文字の形がある");
    }

    [Test]
    public static void Ambiguous_UsesBothSides()
    {
        (string? Before, string? After, string Expected)[] cases =
        [
            ("I love ", null, "sushi"),      // 前が英語
            (null, " is great", "sushi"),    // 後ろが英語
            ("I love ", " is great", "sushi"),
            ("今日は", null, "すし"),         // 前が日本語
            (null, "が好き", "すし"),         // 後ろが日本語
            ("I love ", "が好き", "すし"),     // 食い違うときは日本語
            (null, null, "すし"),             // 分からなければ日本語
        ];
        foreach (var (before, after, expected) in cases)
        {
            var k = new Keyboard();
            k.Host.PrecedingText = before;
            k.Host.FollowingText = after;
            k.Type("sushi");
            Assert.Equal(expected, k.Showing, $"前=「{before}」 後ろ=「{after}」");
        }
    }

    [Test]
    public static void Conversion_UsesTextBeforeCaretAsContext()
    {
        var k = new Keyboard();
        k.Host.PrecedingText = "この本は";
        k.Type("atsui ");
        Assert.Equal("この本は", k.Converter.LastContext, "キャレットの前の日本語を変換エンジンに文脈として渡す");
    }

    [Test]
    public static void Conversion_ContextRulesIke()
    {
        var rules = ContextRules.Load(null);
        Assert.Equal("行け", rules.Choose("いけ", "ねむ学校"), "学校いけ → 行け (池にしない)");
        Assert.Equal("行けよ", rules.Choose("いけよ", "早く"), "助詞付きでも 行け");
        Assert.Equal("池", rules.Choose("いけ", "公園の"), "公園なら 池");
        Assert.Equal(null, rules.Choose("いけん", "学校の"), "意見 を 行けん にしない");
        Assert.Equal(null, rules.Choose("いけない", "学校で"), "いけない を 行けない にしない");
    }

    [Test]
    public static void Conversion_ContextRulesPickTheRightWord()
    {
        var k = new Keyboard();
        k.Host.PrecedingText = "今日の気温は";
        k.Type("atsui ");
        Assert.Equal("暑い", k.Host.View!.Clauses![0], "気温 が前にあれば あつい → 暑い");

        k = new Keyboard();
        k.Host.PrecedingText = "財布の";
        k.Type("kawa ");
        Assert.Equal("革", k.Host.View!.Clauses![0], "財布 が前にあれば かわ → 革");

        k = new Keyboard();
        k.Type("kawa ");
        Assert.Equal("川", k.Host.View!.Clauses![0], "手がかりが無ければ変換エンジンの結果");
    }

    [Test]
    public static void Conversion_LearnsUserChoice()
    {
        var history = new ConversionHistory(null);
        var k = new Keyboard(history: history);
        k.Type("hashiwo ");
        var candidates = k.Host.View!.Candidates;
        var index = candidates.ToList().IndexOf("箸を");
        Assert.True(index > 0, string.Join(",", candidates));
        for (var i = 0; i < index; i++) k.Type(" ");
        k.Type("\n");
        Assert.Equal("箸を", k.Host.Output.Single());

        k = new Keyboard(history: history);
        k.Type("hashiwo ");
        Assert.Equal("箸を", k.Host.View!.Clauses![0], "前に選び直した変換が最初の候補になる");
    }

    [Test]
    public static void CapitalI_IsEnglish()
    {
        // 報告: I want の I が「い」になる。
        var k = new Keyboard();
        k.Type("I");
        Assert.Equal("I", k.Showing, "大文字 1 文字でも英語");
        k.Type(" want ");
        Assert.Equal("I want ", k.Host.Document);
    }

    [Test]
    public static void AutoCorrect_JapaneseToEnglishAfterCommit()
    {
        // i を Space で変換して確定した後に、はっきり英語の語 (want) が続いたら「I 」に確定し直す。
        var k = new Keyboard();
        k.Type("i want ");
        Assert.Equal("I want ", k.Host.Document, string.Join("|", k.Host.Events));
    }

    [Test]
    public static void AutoCorrect_EnglishToJapaneseAfterCommit()
    {
        var k = new Keyboard();
        k.Host.PrecedingText = "I love ";
        k.Type("sushi ");
        Assert.Equal("sushi ", k.Host.Document, "前が英語なので英語で確定");
        k.Host.PrecedingText = null;
        k.Type("gasuki\n");
        Assert.Equal("すしがすき", k.Host.Document, "後ろに日本語が続いたので日本語に確定し直す");
    }

    [Test]
    public static void AutoCorrect_NotAfterCaretMoved()
    {
        var k = new Keyboard();
        k.Type("i ");
        k.Type("w");
        k.Controller.ForgetLastCommit(); // Meltype を通らないキー (矢印など) が押された
        k.Type("ant ");
        Assert.True(!k.Host.Events.Any(e => e.StartsWith("bs:")), "キャレットが動いたかもしれないので消さない");
    }

    [Test]
    public static void AutoCorrect_NotWhenUserChoseCandidate()
    {
        var k = new Keyboard();
        k.Type("i  "); // 2 回目の Space で候補を選び直した
        k.Type("want ");
        Assert.True(!k.Host.Events.Any(e => e.StartsWith("bs:")), "自分で選んだ変換は直さない");
    }

    [Test]
    public static void Symbols_StartComposition()
    {
        // 報告: かぎかっこが入力できない。
        var cases = new Dictionary<string, string> { ["[kagi]"] = "「かぎ」", ["-"] = "ー", ["/"] = "／", ["z/"] = "・", ["#"] = "＃", ["("] = "（", ["@"] = "＠", [","] = "、",
            // 報告: Shift で打つ記号が全角で打てない、/ が打てない。英語の中では半角のまま。
            ["$%&"] = "＄％＆", ["kyouha(tenki)"] = "きょうは（てんき）", ["hello@example"] = "hello@example",
            // 報告: ca / cu / co で か く こ
            ["cacuco"] = "かくこ",
        };
        foreach (var (typed, expected) in cases)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(expected, k.Showing, $"「{typed}」");
        }
    }

    [Test]
    public static void Symbols_HalfWidthCandidate()
    {
        // 報告: Space を続けて押して、記号も半角で出せるように。
        var k = new Keyboard();
        k.Type("# ");
        Assert.True(k.Host.View!.Candidates.Contains("#"), "＃ の候補に # がある: " + string.Join(",", k.Host.View.Candidates));
    }

    [Test]
    public static void CapitalizedWord_FollowedByJapanese()
    {
        // 報告: 今日はAutoIMEnotesutowosimasu が全部英字になる。大文字で始まる語の後ろの日本語は日本語にする。
        var cases = new Dictionary<string, string>
        {
            ["AutoIMEnotesuto"] = "AutoIMEのてすと", ["Githubdekaku"] = "Githubでかく", ["OKdesu"] = "OKです",
            ["Tokyo"] = "Tokyo", ["Hello"] = "Hello",
            // 報告: I don't → どん't。短縮形は英語。
            ["don't"] = "don't", ["I'm"] = "I'm",
        };
        foreach (var (typed, expected) in cases)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(expected, k.Showing, $"「{typed}」");
        }
    }

    [Test]
    public static void Numbers_StayHalfWidth()
    {
        var k = new Keyboard();
        k.Type("2025");
        Assert.Equal("2025", k.Showing, "数字だけなら半角のまま");
        k.Type(" ");
        Assert.Equal("2025 ", k.Host.Output.Single(), "数字だけなら Space は空白");

        k = new Keyboard();
        k.Type("3ji");
        Assert.Equal("3じ", k.Showing, "数字の後にかなが続けば日本語");
        Assert.Equal("2025年10月", CompositionController.NormalizeHalfWidth("２０２５年１０月", "2025ねん10がつ"), "変換エンジンが全角にした数字は半角に戻す");
        Assert.Equal("１つ", CompositionController.NormalizeHalfWidth("１つ", "ひとつ"), "読みに半角数字が無ければそのまま");
    }

    [Test]
    public static void Candidates_ExpandWithWindowsCandidates()
    {
        var k = new Keyboard(moreCandidates: reading => reading == "かわ" ? ["川", "皮", "河", "革"] : []);
        k.Type("kawa ");
        Assert.True(!k.Host.View!.Candidates.Contains("河"), "打っている間は Windows の候補を取りに行かない");
        k.Type(" ");
        Assert.Equal("皮", k.Host.View!.Clauses![0], "候補を切り替え始めたら Windows の候補一覧から");
        Assert.True(k.Host.View.Candidates.Contains("河") && k.Host.View.Candidates.Contains("革"), string.Join(",", k.Host.View.Candidates));
    }

    [Test]
    public static void Backspace_ThenRetype_RereadsRomaji()
    {
        // 報告: test の最後の t を消して u → 「てsう」になる。
        var k = new Keyboard();
        k.Type("test\bu");
        Assert.Equal("てす", k.Showing, "読めなかった s も次の文字と合わせて読み直す");
    }

    [Test]
    public static void LetterThenJapanese_IsNotWholeEnglish()
    {
        // 報告: sだけが と打つと sdakega になる。
        var k = new Keyboard();
        k.Type("sdakega");
        Assert.Equal("sだけが", k.Showing);
        Assert.Equal("stackoverflow", new Func<string?>(() => { var s = new Keyboard(); s.Type("stackoverflow"); return s.Showing; })(),
            "続きもローマ字として読めない語は英単語のまま");
    }

    [Test]
    public static void HalfWidth_IsKeptAfterConversion()
    {
        Assert.Equal("sだけが", CompositionController.NormalizeHalfWidth("ｓだけが", "sだけが"));
        Assert.Equal("2025年", CompositionController.NormalizeHalfWidth("２０２５年", "2025ねん"));
        Assert.Equal("ＡＢＣ", CompositionController.NormalizeHalfWidth("ＡＢＣ", "えーびーしー"), "読みに英数字が無ければ全角のまま");
    }

    [Test]
    public static void UserDictionary_WinsOverEngine()
    {
        var dictionary = new UserDictionary(null);
        Assert.True(dictionary.Add("きごうとう", "記号等") is null, "登録できる");
        Assert.True(dictionary.Add("き", "記") is not null, "1 文字の読みは登録できない");

        var k = new Keyboard(userDictionary: dictionary);
        k.Host.PrecedingText = "文章を「記号等」含め、"; // 変換エンジンが き|ごうとう と区切ってしまう文脈
        k.Type("kigoutoufukume ");
        Assert.Equal("記号等", k.Host.View!.Clauses![0], "登録した読みの部分は、変換エンジンの区切りに関係なく登録した単語");
        Assert.Equal(2, k.Host.View.Clauses.Count, "残り (ふくめ) は別の文節");

        k = new Keyboard(live: true, userDictionary: dictionary);
        k.Type("kigoutou");
        Assert.Equal("記号等", k.Showing, "ライブ変換でも使う");
    }

    [Test]
    public static void UserDictionary_SavesAndLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"meltype-userdict-{Guid.NewGuid():N}.txt");
        try
        {
            var dictionary = new UserDictionary(path);
            dictionary.Add("きごうとう", "記号等");
            dictionary.Add("おーとあいえむいー", "Meltype");
            var loaded = new UserDictionary(path);
            Assert.Equal("記号等", loaded.Lookup("きごうとう").Single());
            Assert.Equal("Meltype", loaded.Lookup("おーとあいえむいー").Single());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public static void Context_DoesNotChangeClauseBoundaries()
    {
        // 報告: 記号等 がどうしても 機強盗 になる (前に「記号等」があると変換エンジンが き|ごうとう と区切る)。
        var k = new Keyboard();
        k.Host.PrecedingText = "文章を「記号等」含め、";
        k.Type("kigoutou ");
        Assert.Equal("記号|等", string.Join("|", k.Host.View!.Clauses!), "文脈で区切りが変わるなら文脈なしの結果を使う");
    }

    [Test]
    public static void ParticleStart_GetsContext()
    {
        // 報告: ○○になってしまう → 「になってしまう」が「担ってしまう」になる。
        var k = new Keyboard();
        k.Type("ninatteshimau ");
        Assert.Equal("これ", k.Converter.LastContext, "前の文脈が無いときは仮の文脈で「に」を助詞として読ませる");

        k = new Keyboard();
        k.Host.PrecedingText = "〇〇";
        k.Type("ninatteshimau ");
        Assert.Equal("〇〇", k.Converter.LastContext, "○ などの記号も日本語の文脈として渡す");

        k = new Keyboard();
        k.Type("hashiru ");
        Assert.True(k.Converter.LastContext is null, "に 以外で始まる読みには仮の文脈を付けない (はしる → は知る を防ぐ)");
    }

    [Test]
    public static void Learning_SkipsSingleKana()
    {
        var history = new ConversionHistory(null);
        var k = new Keyboard(history: history);
        k.Type("ki  \n"); // き を変換して候補を選び直す
        Assert.Equal(0, history.Count, "1 文字の読みは覚えない (き → 記 が きごうとう まで巻き込むため)");
    }

    [Test]
    public static void Clauses_ShiftArrowResizes()
    {
        var k = new Keyboard();
        k.Type("tanniwotoru ");
        k.Key(VirtualKeys.LShift);
        k.Press(VirtualKeys.Left);
        Assert.Equal("単位|をとる", string.Join("|", k.Host.View!.Clauses!), "Shift+← で文節を 1 文字縮め、残りは次の文節へ");
        k.Press(VirtualKeys.Right);
        Assert.Equal("たんいを|とる", string.Join("|", k.Host.View.Clauses!), "Shift+→ で 1 文字伸ばす");
        k.Key(VirtualKeys.LShift, up: true);
        k.Type("\n");
        Assert.Equal("たんいをとる", k.Host.Output.Single());
    }

    [Test]
    public static void AmbiguousWord_FollowsEnglishContext()
    {
        var k = new Keyboard();
        k.Type("hello ");
        k.Type("sushi");
        Assert.Equal("sushi", k.Showing, "英語の後なら sushi は英語");
        k.Type(" ");
        Assert.Equal("hello |sushi ", string.Join("|", k.Host.Output), "英語なので Space は空白");
    }

    [Test]
    public static void AmbiguousWord_FollowsJapaneseContext()
    {
        var k = new Keyboard();
        k.Type("kyouha\n");
        k.Type("sushi");
        Assert.Equal("すし", k.Showing, "日本語の後なら sushi は日本語");

        k = new Keyboard();
        k.Type("hello ");
        k.Type("sushiga");
        Assert.Equal("すしが", k.Showing, "英語の後でも、後ろに日本語が続けば日本語");
    }

    [Test]
    public static void Space_AfterEnglishWord_InsertsSpace()
    {
        var k = new Keyboard();
        k.Type("kyouhagoogle ");
        Assert.Equal("今日はgoogle ", k.Host.Output.Single(), "英単語で終わっていれば、変換ではなく確定して空白");
    }

    [Test]
    public static void JapaneseSentences_StayJapanese()
    {
        foreach (var sentence in new[]
        {
            "watashihagakuseidesu", "ashitahaamedesu", "kyouhaiitenkidesune", "sumimasenkakuninshimasu",
            "kanojohasushigasuki", "nihongonobenkyou", "arigatougozaimasu", "kaishaniikimasu", "itsumoarigatou",
            "tomodachitoasobu", "shiryouwookurimasu", "mondaihaarimasen",
        })
        {
            var k = new Keyboard();
            k.Type(sentence + "\n"); // Enter で確定 (末尾の n も ん になる)
            Assert.True(k.Host.Output.Single().All(c => !char.IsAsciiLetter(c)), $"「{sentence}」に英字が混ざった: {k.Host.Output.Single()}");
        }
    }

    [Test]
    public static void LiveConversion_ConvertsWhileTyping()
    {
        var k = new Keyboard(live: true);
        k.Type("kyou");
        Assert.Equal("きょう", k.Showing, "短いうちはかなのまま (的外れな漢字を出さない)");
        k.Type("ha");
        Assert.Equal("今日は", k.Showing, "4 文字以上になったら Space を押さなくても漢字で表示");
        k.Type("google");
        Assert.Equal("今日はgoogle", k.Showing, "英語の部分は変換しない");
        k.Type("dekensaku\n");
        Assert.Equal("今日はgoogleで検索", k.Host.Output.Single());
    }

    [Test]
    public static void LiveConversion_SpaceShowsAlternatives()
    {
        var k = new Keyboard(live: true);
        k.Type("kyou ");
        Assert.True(k.Host.View!.Converting, "Space で候補一覧");
        k.Type(" ");
        Assert.Equal("きょう", k.Showing, "次の候補はかなのまま");
        k.Type("\n");
        Assert.Equal("きょう", k.Host.Output.Single());
    }

    [Test]
    public static void Backspace_DeletesOneKanaAtATime()
    {
        var k = new Keyboard();
        k.Type("kyou\b");
        Assert.Equal("きょ", k.Showing, "ローマ字 1 文字ではなく、かな 1 音ずつ消える");
        k.Type("\b");
        Assert.True(k.Showing is null, "きょ も 1 回で消える");

        k.Type("kitte\b");
        Assert.Equal("きっ", k.Showing);

        k = new Keyboard();
        k.Type("ky\b");
        Assert.Equal("k", k.Showing, "まだ音になっていない子音は 1 文字ずつ");

        k = new Keyboard();
        k.Type("google\b");
        Assert.Equal("googl", k.Showing, "英字は 1 文字ずつ");
    }

    [Test]
    public static void EnglishWords_AreShownAsEnglish()
    {
        foreach (var word in new[] { "github", "hello", "typescript", "npm", "localhost", "zoom", "git", "test", "iphone", "stackoverflow" })
        {
            var k = new Keyboard();
            k.Type(word);
            Assert.Equal(word, k.Showing, $"「{word}」");
        }
    }

    [Test]
    public static void JapaneseWords_AreShownAsKana()
    {
        var expected = new Dictionary<string, string>
        {
            ["watashi"] = "わたし", ["arigatou"] = "ありがとう", ["kana"] = "かな", ["sushi"] = "すし", ["tesuto"] = "てすと",
            ["make"] = "まけ", ["ra-men"] = "らーめn", ["nihon"] = "にほn", ["konnichiwq"] = "こんいちwq", ["tanni"] = "たんい",
        };
        foreach (var (typed, kana) in expected)
        {
            var k = new Keyboard();
            k.Type(typed);
            Assert.Equal(kana, k.Showing, $"「{typed}」");
        }
    }

    [Test]
    public static void FinalN_BecomesN_OnCommit()
    {
        var k = new Keyboard();
        k.Type("nihon\n");
        Assert.Equal("にほん", k.Host.Output.Single());
    }

    [Test]
    public static void Capitalized_IsEnglish()
    {
        var k = new Keyboard();
        k.Type("Tokyo");
        Assert.Equal("Tokyo", k.Showing);
    }

    [Test]
    public static void Space_ConvertsThenCyclesCandidates()
    {
        var k = new Keyboard();
        k.Type("kyou ");
        Assert.Equal("今日", k.Showing);
        Assert.True(k.Host.View!.Converting, "変換中");
        k.Type(" ");
        Assert.Equal("きょう", k.Showing, "もう一度 Space で次の候補");
        k.Type(" ");
        Assert.Equal("キョウ", k.Showing);
        k.Type("\n");
        Assert.Equal("キョウ", k.Host.Output.Single());
    }

    [Test]
    public static void TypingAfterConversion_CommitsAndStartsNew()
    {
        var k = new Keyboard();
        k.Type("nihongo wo");
        Assert.Equal("日本語", k.Host.Output.Single(), "変換中に次の文字を打つと確定");
        Assert.Equal("を", k.Showing);
    }

    [Test]
    public static void Space_OnEnglish_CommitsWithSpace()
    {
        var k = new Keyboard();
        k.Type("hello world\n");
        Assert.Equal("hello |world", string.Join("|", k.Host.Output));
    }

    [Test]
    public static void Backspace_EditsAndEscapeCancels()
    {
        var k = new Keyboard();
        k.Type("kak\b");
        Assert.Equal("か", k.Showing);
        k.Type("\b\b");
        Assert.True(k.Showing is null && !k.Gate.IsCaptured, "空になったら閉じる");
        Assert.Equal(0, k.Host.Output.Count);

        k.Type("abc");
        k.Press(VirtualKeys.Escape);
        Assert.True(k.Showing is null, "Esc で取り消し");
        Assert.Equal(0, k.Host.Output.Count);
    }

    [Test]
    public static void FunctionKeys_ChangeDisplay()
    {
        var k = new Keyboard();
        k.Type("tesuto");
        k.Press(VirtualKeys.F7);
        Assert.Equal("テスト", k.Showing);
        k.Press(VirtualKeys.F10);
        Assert.Equal("tesuto", k.Showing);
        k.Press(VirtualKeys.OemAuto); // 半角/全角 で日本語⇔英字
        Assert.Equal("てすと", k.Showing);
    }

    [Test]
    public static void OtherKeys_CommitFirstThenPassThroughInOrder()
    {
        var k = new Keyboard();
        k.Type("kana");
        k.Press(VirtualKeys.Tab);
        Assert.Equal("text:かな|down:09|passed-up:09", string.Join("|", k.Host.Events), "確定後の キーアップ は関所を通らず直接届く");
    }

    [Test]
    public static void CtrlShortcut_CommitsFirst()
    {
        var k = new Keyboard();
        k.Type("kana");
        k.Key(VirtualKeys.LControl);
        k.Press('C');
        k.Key(VirtualKeys.LControl, up: true);
        Assert.Equal("text:かな|down:A2|passed:43|passed-up:43|passed-up:A2", string.Join("|", k.Host.Events), "確定 → Ctrl を送る → 以降は直接アプリへ");
        Assert.True(!k.Gate.IsCaptured, "ショートカットの後は横取りをやめる");
    }

    [Test]
    public static void AfterShortcut_NextWordStillComposes()
    {
        var k = new Keyboard();
        k.Type("kana");
        k.Key(VirtualKeys.LControl);
        k.Press('S');
        k.Key(VirtualKeys.LControl, up: true);
        k.Type("kyou");
        Assert.Equal("きょう", k.Showing, "ショートカットの後も普通に変換ボックスが使える");
    }

    [Test]
    public static void ShiftArrow_KeepsShift()
    {
        var k = new Keyboard();
        k.Type("hello"); // 英字だけのときの矢印はキャレット移動 (日本語なら文節の選択になる)
        k.Key(VirtualKeys.LShift);
        k.Press(0x25); // ←
        k.Key(VirtualKeys.LShift, up: true);
        Assert.Equal("text:hello|down:A0|down:25|passed-up:25|passed-up:A0", string.Join("|", k.Host.Events), "範囲選択のための Shift はアプリに届く");
    }

    [Test]
    public static void MouseClick_CommitsBeforeClicking()
    {
        var k = new Keyboard();
        k.Type("kana");
        Assert.True(k.Gate.OnMouseButton(new MouseButtonEvent(0x201, 10, 20, 0)), "変換中のクリックは一旦止める");
        k.Controller.Pump();
        Assert.Equal("text:かな|mouse:201", string.Join("|", k.Host.Events), "確定してからクリックを再生");
        Assert.True(!k.Gate.OnMouseButton(new MouseButtonEvent(0x202, 10, 20, 0)), "確定後のクリックは素通し");
    }

    [Test]
    public static void KeysBeforeCapture_AreNotSwallowed()
    {
        var k = new Keyboard();
        Assert.True(!k.Key(VirtualKeys.Space), "変換ボックスが無いときの Space は素通し");
        Assert.True(!k.Key(VirtualKeys.Tab), "Tab など文字を生まないキーは素通し");
        Assert.True(k.Key(0x31), "数字は変換ボックスに入る (数字だけなら半角のまま、Space で空白)");
    }

    [Test]
    public static void KeyUpOfKeyPressedBeforeCapture_IsReplayed()
    {
        // Shift を押したまま大文字で打ち始めた場合、Shift の押下は関所の前にアプリへ届いている。
        var k = new Keyboard();
        k.Key('T');
        k.Key(VirtualKeys.LShift, up: true);
        Assert.True(k.Host.Events.Contains("up:A0"), "離したことを伝えないと Shift が押しっぱなしになる");
    }
}
