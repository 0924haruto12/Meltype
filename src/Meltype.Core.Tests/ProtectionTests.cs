// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Composition;
using Meltype.Config;
using Meltype.Input;

namespace Meltype.Tests;

/// <summary>
/// MT-001 / MT-002: メンション・URL・メール・パスの保護範囲と、確定操作ごとの契約 (acceptance/acceptance_cases.json)。
/// </summary>
internal static class ProtectionTests
{
    // ---- scanner: 保護する ----
    [Test]
    public static void Scanner_FindsUrlEmailPathMention()
    {
        foreach (var (raw, kind) in new[]
                 {
                     ("https://example.com/path?q=nihongo&x=a%2Fb#konnichiwa", ProtectedKind.Url),
                     ("file:///home/taro/main.ts", ProtectedKind.Url),
                     ("https://example.com/", ProtectedKind.Url),
                     ("https://example.com/a(b)c", ProtectedKind.Url),
                     ("taro.yamada+tag@example.com", ProtectedKind.Email),
                     ("taro.yamada@", ProtectedKind.Email),
                     ("../src/main.ts", ProtectedKind.PosixPath),
                     ("~/project/main.ts", ProtectedKind.PosixPath),
                     ("./src/", ProtectedKind.PosixPath),
                     ("C:/Users/taro/project", ProtectedKind.WindowsPath),
                     ("\\\\server\\share\\nihongo.txt", ProtectedKind.WindowsPath),
                     ("@User_123", ProtectedKind.Mention),
                 })
        {
            var spans = ProtectedSpanScanner.Scan(raw);
            Assert.True(spans.Count == 1, $"1 つの保護区間: {raw} (実際 {spans.Count})");
            Assert.Equal(kind, spans[0].Kind, raw);
            Assert.Equal(0, spans[0].Start, raw);
            Assert.Equal(raw.Length, spans[0].End, raw);
            Assert.True(ProtectedSpanScanner.CoversWhole(raw), $"全体を保護: {raw}");
        }
    }

    // ---- scanner: 保護しない ----
    [Test]
    public static void Scanner_LeavesOrdinaryTextAlone()
    {
        foreach (var raw in new[]
                 {
                     "a/b", "1/2", "///", "@", "/", ":", "kyouhagithubnipushshita",
                     "konnichiwa.", "OPENAI_API_KEY", "camelCase", "2026-10-05", "hello", "upah_setu",
                 })
        {
            var spans = ProtectedSpanScanner.Scan(raw);
            Assert.Equal(0, spans.Count, $"保護しない: {raw}");
        }
    }

    [Test]
    public static void Scanner_ReanalyseAfterEdit()
    {
        // @ を消すと保護が外れる (保護フラグだけが残らない)。
        Assert.True(ProtectedSpanScanner.CoversWhole("@kuraido"), "@kuraido は保護する");
        Assert.Equal(0, ProtectedSpanScanner.Scan("@").Count, "@ だけは保護しない");
        // ドメインを打ち始めるまでも暫定保護する。
        Assert.True(ProtectedSpanScanner.CoversWhole("taro.yamada@"), "taro.yamada@ は暫定保護する");
        Assert.Equal(0, ProtectedSpanScanner.Scan("taro.yamada").Count, "taro.yamada だけは保護しない");
    }

    // ---- 確定契約 (MeltypeSession は Linux/Mac アダプターと同じ経路) ----

    private static MeltypeSession Create(LanguageMemory? languages = null)
    {
        CompositionTests.Detector.SpellChecker = Detection.BuiltInWordChecker.Shared;
        // 製品の CreateDefault と同じく、候補辞書・誤字補正・文脈辞書を入れる (実経路と同じ条件で検査する)。
        var options = new CompositionOptions
        {
            Candidates = CandidateDictionary.Load(null),
            ContextRules = ContextRules.Load(null),
            History = new ConversionHistory(null),
            Misspellings = MisspellingDictionary.Load(null),
            RomajiTypos = RomajiTypoCorrector.Load(CompositionTests.Detector.Romaji),
            Languages = languages,
        };
        return new MeltypeSession(CompositionTests.Detector, new CompositionTests.FakeConverter(), options, () => new Settings());
    }

    private static SessionResult TypeOne(MeltypeSession session, char c)
    {
        // Linux ラッパー (ibus-engine-meltype) と同じく、英字は A-Z の仮想キー、他の文字は他文字キー (7)。
        var vk = c switch
        {
            ' ' => VirtualKeys.Space,
            '\n' => VirtualKeys.Return,
            _ when char.IsAsciiLetter(c) => char.ToUpperInvariant(c),
            _ when char.IsAsciiDigit(c) => c,
            _ => 0x07,
        };
        return session.HandleKey(vk, c is ' ' or '\n' ? null : c, char.IsAsciiLetterUpper(c), false, false, false);
    }

    private static void TypeText(MeltypeSession session, string text)
    {
        foreach (var c in text) TypeOne(session, c);
    }

    [Test]
    public static void Protected_MentionSpaceCommitsRawPlusHalfWidthSpace()
    {
        var session = Create();
        TypeText(session, "@kuraido");
        var space = TypeOne(session, ' ');
        Assert.True(space.Consumed, "Space は IME が消費する (アプリへ送らない)");
        Assert.Equal("@kuraido ", space.Commits.Single().Text);
        Assert.True(space.View is null, "確定したら変換ボックスを閉じる");
    }

    [Test]
    public static void Protected_EnterCommitsRawWithoutNewline()
    {
        var session = Create();
        TypeText(session, "https://example.com?q=konnichiwa");
        var enter = TypeOne(session, '\n');
        Assert.True(enter.Consumed, "Enter は IME が消費する (改行を入れない)");
        Assert.Equal("https://example.com?q=konnichiwa", enter.Commits.Single().Text);
    }

    [Test]
    public static void Protected_TabCommitsRawAndPassesThrough()
    {
        var session = Create();
        TypeText(session, "taro.yamada@example.com");
        // Linux/Mac は Tab を文字なし (ch=0) で渡す。
        var tab = session.HandleKey(VirtualKeys.Tab, null, false, false, false, false);
        Assert.True(!tab.Consumed, "Tab はアプリへ 1 回だけ渡す");
        Assert.Equal("taro.yamada@example.com", tab.Commits.Single().Text);
    }

    [Test]
    public static void Protected_FocusCommitsRaw()
    {
        var session = Create();
        TypeText(session, "/home/taro/project");
        var focus = session.CommitPending();
        Assert.Equal("/home/taro/project", focus.Commits.Single().Text);
    }

    [Test]
    public static void Protected_WindowsPathAndEmailKeepBackslashAndAscii()
    {
        var session = Create();
        TypeText(session, "C:\\Users\\taro\\project");
        Assert.Equal("C:\\Users\\taro\\project", session.HandleKey(VirtualKeys.Return, null, false, false, false, false).Commits.Single().Text);
    }

    [Test]
    public static void Protected_BackspaceDeletesRawCharacters()
    {
        var session = Create();
        TypeText(session, "@kuraido");
        for (var i = 0; i < 7; i++) session.HandleKey(VirtualKeys.Back, null, false, false, false, false);
        var view = session.HandleKey(VirtualKeys.Right, null, false, false, false, false).View;
        Assert.True(view is null || view.Text == "@", $"7 回の Backspace で @ だけ残る (実際 {view?.Text})");
        Assert.Equal("@", session.HandleKey(VirtualKeys.Return, null, false, false, false, false).Commits.Single().Text);
    }

    [Test]
    public static void NormalJapanese_StillConverts()
    {
        var session = Create();
        TypeText(session, "kyouha");
        var space = TypeOne(session, ' ');
        Assert.Equal("今日は", space.View?.Text, "普通の日本語の Space は変換のまま");
    }

    [Test]
    public static void ExplicitOverride_WinsOverProtection()
    {
        // F10 (半角英数) を明示したら、保護していてもその指定を優先する。
        var session = Create();
        TypeText(session, "@kuraido");
        session.HandleKey(VirtualKeys.F10, null, false, false, false, false);
        Assert.Equal("@kuraido ", TypeOne(session, ' ').Commits.Single().Text);
        // F9 (全角英数) も同様。次の token に指定が漏れない。
        var session2 = Create();
        TypeText(session2, "@kuraido");
        session2.HandleKey(VirtualKeys.F9, null, false, false, false, false);
        var committed = TypeOne(session2, '\n').Commits.Single().Text;
        Assert.Equal(CompositionText.ToFullWidth("@kuraido"), committed);
    }

    [Test]
    public static void Protected_IsNotLearnedOrCorrected()
    {
        var languages = new LanguageMemory(null);
        var session = Create(languages);
        TypeText(session, "https://example.com/?token=synthetic_test_only");
        TypeOne(session, '\n');
        // 保護した原文は学習記憶に残さない。
        Assert.True(languages.Get("https://example.com/?token=synthetic_test_only") is null, "保護原文を学習しない");
    }
}
