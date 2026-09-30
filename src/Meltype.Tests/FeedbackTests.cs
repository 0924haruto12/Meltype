// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Composition;
using Meltype.Config;
using Meltype.Detection;
using Meltype.Input;

namespace Meltype.Tests;

/// <summary>協力者のテストで報告された問題 (2026-09) と、判定の強さ (Aggressive / Balanced / Conservative / Manual)。</summary>
internal static class FeedbackTests
{
    private static string Showing(string typed, string? before = null, DetectionLevel level = DetectionLevel.Balanced)
    {
        var k = new CompositionTests.Keyboard { Level = level };
        k.Host.PrecedingText = before;
        k.Type(typed);
        return k.Showing ?? "";
    }

    /// <summary>Windows のスペルチェッカーを使って打つ (使えない環境では null)。</summary>
    private static string? TypeWithSpellChecker(string typed, DetectionLevel level = DetectionLevel.Balanced)
    {
        if (!WindowsSpellChecker.Shared.IsAvailable) return null;
        CompositionTests.Detector.SpellChecker = WindowsSpellChecker.Shared;
        try
        {
            var k = new CompositionTests.Keyboard { Level = level };
            k.Type(typed);
            return k.Host.Document;
        }
        finally
        {
            CompositionTests.Detector.SpellChecker = null;
        }
    }

    [Test]
    public static void SmallKana_WithXAndL()
    {
        Assert.Equal("ぁ", Showing("xa"));
        Assert.Equal("ぁ", Showing("la"));
        Assert.Equal("まぁ", Showing("mala"), "mala は英語ではなく まぁ");
    }

    [Test]
    public static void ShiftedSingleConsonant_StaysUppercase()
    {
        foreach (var c in "WRTYPSDGHKLZXVBNM")
        {
            Assert.Equal(c.ToString(), Showing(c.ToString()), $"Shift+{c}");
        }
        Assert.Equal("かW", Showing("kaW"));
    }

    [Test]
    public static void ShiftedSymbols_StartComposition()
    {
        Assert.Equal("！", Showing("!"));
        Assert.Equal("？", Showing("?"));
        Assert.Equal("～", Showing("~"));
        Assert.Equal("!", Showing("!", before: "Hello"), "英文の後は半角");
    }

    [Test]
    public static void ShortParticles_AfterEnglishWord_AreJapanese()
    {
        foreach (var (typed, expected) in new[] { ("no", "の"), ("to", "と"), ("ga", "が") })
        {
            Assert.Equal(expected, Showing(typed, before: "GitHub"), $"GitHub + {typed}");
            Assert.Equal(expected, Showing(typed, before: "今日は GitHub "), $"今日は GitHub + {typed}");
        }
        Assert.Equal("no", Showing("no", before: "GitHub", level: DetectionLevel.Aggressive), "積極的なら英語");
    }

    [Test]
    public static void ShortWords_InEnglishSentence_AreEnglish()
    {
        Assert.Equal("to", Showing("to", before: "I want "));
        Assert.Equal("a", Showing("a", before: "this is "));
        Assert.Equal("is", Showing("is", before: "this "), "子音で終わる語は片側が英語なら英語");
    }

    [Test]
    public static void Levels_ChangeHowEagerlyEnglishIsShown()
    {
        Assert.Equal("sushi", Showing("sushi", before: "I like "), "英文の後");
        Assert.Equal("すし", Showing("sushi", before: "I ", level: DetectionLevel.Conservative), "慎重: 1 語だけの文脈では英語にしない");
        Assert.Equal("google", Showing("google", level: DetectionLevel.Conservative), "慎重でもローマ字として読めない英単語は英語");
        Assert.Equal("あまぞn", Showing("amazon", level: DetectionLevel.Conservative), "慎重: ローマ字として読める固有名詞は日本語のまま");
    }

    [Test]
    public static void Manual_OnlySuggests_TabAccepts()
    {
        var k = new CompositionTests.Keyboard { Level = DetectionLevel.Manual };
        k.Type("google");
        Assert.Equal("ごおgぇ", k.Showing, "手動: 自動では英字にしない");
        Assert.True(k.Host.View!.Hint.Contains("Tab → google"), $"提案を表示: {k.Host.View.Hint}");
        k.Press(VirtualKeys.Tab);
        Assert.Equal("google", k.Showing, "Tab で提案どおり英字");
        k.Type("\n");
        Assert.Equal("google", k.Host.Document);

        Assert.Equal("Google", Showing("Google", level: DetectionLevel.Manual), "Shift で打った大文字始まりは手動でも英語");
    }

    [Test]
    public static void EnglishSentences_WithSpellChecker()
    {
        foreach (var sentence in new[] { "I want to go to the park", "make sure you have time", "the meeting is at nine", "my name is taro", "this is a pen" })
        {
            if (TypeWithSpellChecker(sentence + "\n") is not { } result) return;
            Assert.Equal(sentence, result, "英文");
        }
    }

    [Test]
    public static void JapaneseSentences_WithSpellChecker()
    {
        foreach (var (typed, expected) in new[]
        {
            ("kore ha pen desu", "これはぺんです"),
            ("GitHub no repo", "GitHub のれぽ"),
            ("Google to Apple", "Google とApple"),
            ("watashi ha sushi ga suki", "わたしはすしがすき"),
            ("mala", "まぁ"),
        })
        {
            if (TypeWithSpellChecker(typed + "\n") is not { } result) return;
            Assert.Equal(expected, result, "日本語の文");
        }
    }
}

internal static class KanaInputTests
{
    private static CompositionTests.Keyboard Kana(DetectionLevel level = DetectionLevel.Balanced) => new() { Kana = true, Level = level };

    [Test]
    public static void KanaKeys_ProduceKana()
    {
        var k = Kana();
        k.TypeKanaKeys("tu");
        Assert.Equal("かな", k.Showing, "T = か, U = な");
        k.TypeKanaKeys("t@");
        Assert.Equal("かなが", k.Showing, "濁点は直前のかなに付く");
        k.Press(VirtualKeys.Back);
        Assert.Equal("かな", k.Showing, "BackSpace で が を 1 文字消す");
    }

    [Test]
    public static void KanaKeys_ShiftGivesSmallKanaAndPunctuation()
    {
        var k = Kana();
        k.TypeKeys((0x45, true), (0x5A, true), (0xBC, true), (0xBE, true), (0x33, false), (0x33, true));
        Assert.Equal("ぃっ、。あぁ", k.Showing);
        k.TypeKeys((0xBC, false));
        Assert.Equal("ぃっ、。あぁね", k.Showing, "Shift なしの , は ね");
    }

    [Test]
    public static void KanaInput_EnglishWordsAreShownAsEnglish()
    {
        var k = Kana();
        k.TypeKanaKeys("google");
        Assert.Equal("google", k.Showing, "打ったキーが英単語で、かなとしては日本語にならない");
        k.Type("\n");
        Assert.Equal("google", k.Host.Document);

        var japanese = Kana();
        japanese.TypeKanaKeys("byiaf");
        Assert.Equal("こんにちは", japanese.Showing);
    }

    [Test]
    public static void KanaInput_FollowsLevels()
    {
        var manual = Kana(DetectionLevel.Manual);
        manual.TypeKanaKeys("google");
        Assert.Equal("きららきりい", manual.Showing, "手動: 自動では英字にしない");
        Assert.True(manual.Host.View!.Hint.Contains("Tab → google"), manual.Host.View.Hint);

        var shift = Kana(DetectionLevel.Manual);
        shift.TypeKanaKeys("Google");
        Assert.Equal("Google", shift.Showing, "Shift で打った大文字始まりは英語");
    }
}

internal static class MisspellingTests
{
    [Test]
    public static void Misspelling_IsSuggestedAndFixedWithTab()
    {
        var k = new CompositionTests.Keyboard();
        k.Type("buresureddo");
        Assert.Equal("ぶれすれっど", k.Showing);
        Assert.True(k.Host.View!.Hint.Contains("もしかして: ブレスレット"), k.Host.View.Hint);
        k.Press(VirtualKeys.Tab);
        Assert.Equal("ぶれすれっと", k.Showing, "Tab で正しい読みに直す");
        Assert.True(!k.Host.View!.Hint.Contains("もしかして"), "直した後は出ない");
        k.Type("\n");
        Assert.Equal("ぶれすれっと", k.Host.Document);
    }

    [Test]
    public static void Misspelling_ListedPairsAndSpellingVariants()
    {
        foreach (var (typed, right) in new[]
        {
            ("shumire-shon", "シミュレーション"),
            ("komyunike-shon", null),
            ("kominyuke-shon", "コミュニケーション"),
            ("bure-suretto", "ブレスレット"),
            ("figiasuke-to", "フィギュア"),
            ("kyouhaiitenki", null),
            ("buresurettowokau", null),
        })
        {
            var k = new CompositionTests.Keyboard();
            k.Type(typed);
            var hint = k.Host.View!.Hint;
            if (right is null) Assert.True(!hint.Contains("もしかして"), $"{typed}: 誤りではない ({hint})");
            else Assert.True(hint.Contains($"もしかして: {right}"), $"{typed}: {hint}");
        }
    }

    [Test]
    public static void Misspelling_TabWhileConverting_Reconverts()
    {
        var k = new CompositionTests.Keyboard();
        k.Type("buresureddo ");
        Assert.True(k.Host.View!.Converting, "Space で変換中");
        Assert.True(k.Host.View.Hint.Contains("もしかして: ブレスレット"), k.Host.View.Hint);
        k.Press(VirtualKeys.Tab);
        Assert.True(k.Host.View!.Converting, "直して変換し直す");
        Assert.Equal("ぶれすれっと", k.Showing);
    }
}

internal static class CodeProfileTests
{
    [Test]
    public static void LineTracker_FollowsTypingAndResets()
    {
        var line = new LineTracker();
        Assert.True(line.Text is null, "最初は分からない");
        line.NewLine();
        line.Append("x = 1 ");
        Assert.Equal(LineKind.Code, LineContext.Classify(line.Text!));
        line.Append("// ");
        Assert.Equal(LineKind.Comment, LineContext.Classify(line.Text!), "// の後はコメント");
        line.Backspace();
        line.Backspace();
        line.Backspace();
        Assert.Equal("x = 1 ", line.Text);
        line.NewLine();
        Assert.Equal("", line.Text, "改行で新しい行");
        line.Invalidate();
        Assert.True(line.Text is null, "キャレットが動いたら分からない");
        line.SetFromText("first line\r\n    print(\"こん");
        Assert.Equal(LineKind.String, LineContext.Classify(line.Text!), "UI Automation で読んだ行の最後の改行より後ろを使う");
    }

    [Test]
    public static void Settings_CodeAppsAreCode()
    {
        var settings = new Settings();
        Assert.Equal(AppProfile.Code, settings.ProfileFor("Code.exe"));
        Assert.Equal(AppProfile.Code, settings.ProfileFor("windowsterminal.exe"), "大文字小文字は区別しない");
        Assert.Equal(AppProfile.General, settings.ProfileFor("chrome.exe"));
        Assert.Equal(AppProfile.Code, settings.Clone().ProfileFor("Code.exe"), "複製しても種類を保つ");

        // v3 の設定ファイル (種類が無い) を読み込むと、コードエディター・ターミナルが「コード」になる。ユーザーが OFF にしたものは OFF のまま。
        var old = new Settings { SettingsVersion = 3, AppRules = [new AppRule { Process = "Code.exe", Enabled = false }, new AppRule { Process = "chrome.exe" }] };
        old.Migrate();
        Assert.Equal(AppProfile.Code, old.ProfileFor("Code.exe"));
        Assert.True(!old.IsAppEnabled("Code.exe"), "OFF のまま");
        Assert.Equal(AppProfile.Code, old.ProfileFor("pwsh.exe"), "足りないコードアプリを追加");
        Assert.Equal(AppProfile.General, old.ProfileFor("chrome.exe"));
    }
}

internal static class LanguageLearningTests
{
    [Test]
    public static void F10_TeachesEnglish_ThenUsedInContext()
    {
        var memory = new LanguageMemory(null);
        CompositionTests.Detector.Memory = memory;
        try
        {
            var k = new CompositionTests.Keyboard(languages: memory);
            k.Type("api");
            Assert.Equal("あぴ", k.Showing, "最初は日本語");
            k.Press(VirtualKeys.F10);
            k.Type("\n");
            Assert.Equal("api", k.Host.Document);
            Assert.Equal(true, memory.Get("api"), "F10 で英字にして確定したので覚える");

            var next = new CompositionTests.Keyboard(languages: memory);
            next.Type("apinoerror\n");
            Assert.Equal("apiのerror", next.Host.Document, "次からは文の中でも英字");
        }
        finally
        {
            CompositionTests.Detector.Memory = null;
        }
    }

    [Test]
    public static void F6_TeachesJapanese()
    {
        var memory = new LanguageMemory(null);
        CompositionTests.Detector.Memory = memory;
        try
        {
            var k = new CompositionTests.Keyboard(languages: memory);
            k.Type("google");
            k.Press(VirtualKeys.F6);
            k.Type("\n");
            Assert.Equal("ごおgぇ", k.Host.Document);
            Assert.Equal(false, memory.Get("google"), "F6 でかなにして確定したので覚える");
            var next = new CompositionTests.Keyboard(languages: memory);
            next.Type("google");
            Assert.Equal("ごおgぇ", next.Showing);
        }
        finally
        {
            CompositionTests.Detector.Memory = null;
        }
    }
}
