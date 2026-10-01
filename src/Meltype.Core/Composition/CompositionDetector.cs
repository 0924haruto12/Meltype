// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Config;
using Meltype.Detection;

namespace Meltype.Composition;

/// <summary>
/// 変換ボックスの中身のうち、どこを英字のまま見せるかを決める。
///
/// 日本語入力中に google と打って「ごおｇぇ」になるのを防ぐのが目的。英語かどうかは区間ごとに判定するので、
/// 「きょうは google」なら google の部分だけが英字になる。区間の区切りはかな 1 音の境目だけ。
/// 未確定のうちは何度でも表示を作り直せるので、ここでの判定は IME 自動切替より積極的でよいが、
/// 既定は日本語で、英語と判断できる根拠があるときだけ英字にする。
/// </summary>
public sealed class CompositionDetector
{
    private readonly RomajiDetector _romaji;
    private readonly DictionaryDetector _japanese;
    private readonly EnglishDetector _english;
    private readonly TypoDetector _typo;
    private readonly ProperNouns _proper;
    private readonly KanaDetector? _kana;

    public CompositionDetector(RomajiDetector romaji, DictionaryDetector japanese, EnglishDetector english, TypoDetector typo, ProperNouns? proper = null,
        KanaDetector? kana = null)
    {
        _kana = kana;
        _romaji = romaji;
        _japanese = japanese;
        _english = english;
        _typo = typo;
        _proper = proper ?? new ProperNouns();
    }

    public static CompositionDetector CreateDefault(string? userDictionaryDirectory = null)
    {
        var romaji = new RomajiDetector();
        var japaneseWords = DictionarySource.Load("japanese.txt", userDictionaryDirectory).ToList();
        var japanese = new DictionaryDetector(japaneseWords, romaji);
        var proper = ProperNouns.Load(userDictionaryDirectory);
        var english = new EnglishDetector(DictionarySource.Load("english.txt", userDictionaryDirectory).Concat(proper.LowercaseWords));
        return new CompositionDetector(romaji, japanese, english, new TypoDetector(japanese.Words), proper, new KanaDetector(japaneseWords, romaji));
    }

    public RomajiDetector Romaji => _romaji;

    /// <summary>普通の英単語の判定に使う Windows のスペルチェッカー。null なら同梱の辞書だけ。</summary>
    public IWordChecker? SpellChecker { get; set; }

    /// <summary>ユーザーが英字 / かなに直して覚えた語 (自動の判定より優先する)。</summary>
    public LanguageMemory? Memory { get; set; }

    public ProperNouns ProperNouns => _proper;

    /// <summary>
    /// 単位列 (+ 入力途中の子音) を英語区間と日本語区間に分ける。
    /// 先頭から見て、ある単位から始まる最長の「英語と言える」区間があればそこを英語にする。
    /// </summary>
    /// <param name="precedingEnglish">入力欄のキャレットの前の確定済みの文字が英語なら true、日本語なら false、分からなければ null。</param>
    /// <param name="followingEnglish">キャレットの後ろの文字が英語なら true、日本語なら false、分からなければ null。</param>
    /// <param name="level">判定の強さ。手動 (Manual) では Shift で打った大文字始まりの語だけを英語にする。</param>
    /// <param name="englishSentence">キャレットの前が英文 (空白で区切った英単語が 2 語以上続いて空白で終わる: "I want ")。
    /// 日本語の文の中の英単語 (GitHub の) より強い英語の根拠として扱う。</param>
    public IReadOnlyList<CompositionSegment> Segment(IReadOnlyList<CompositionUnit> units, string pending, bool? precedingEnglish = null, bool? followingEnglish = null,
        DetectionLevel level = DetectionLevel.Balanced, bool englishSentence = false, bool kanaInput = false, bool final = false)
    {
        var segments = FindSpans(units, pending, precedingEnglish, followingEnglish, level, englishSentence && precedingEnglish == true, kanaInput, final);
        if (kanaInput) return segments;
        // 辞書にない英単語 (stackoverflow など) を最初から打っているなら全体を英語にする。
        // 途中の区間 (… flow) だけを英語にすると「sたcこvえrflow」のようになってしまう。
        // ただし先頭が辞書の英単語として区切れている (github に push) ならその区切りを使う。
        var whole = Raw(units, 0, units.Count) + pending;
        if (level != DetectionLevel.Manual && !segments[0].IsEnglish && Memory?.Get(whole.ToLowerInvariant()) != false && IsUnknownEnglishWord(whole))
        {
            return [new CompositionSegment(true, "", whole)];
        }
        return segments;
    }

    private List<CompositionSegment> FindSpans(IReadOnlyList<CompositionUnit> units, string pending, bool? precedingEnglish, bool? followingEnglish, DetectionLevel level, bool englishSentence, bool kanaInput, bool final)
    {
        var n = units.Count;
        var segments = new List<CompositionSegment>();
        var japaneseStart = 0;
        // 区間の前が英語か: 先頭なら入力欄の確定済みの文字、途中なら直前の区間 (英語区間の直後なら英語、それ以外は日本語)。
        bool? PrecededByEnglish(int start) => start == 0 ? precedingEnglish : segments.Count > 0 && segments[^1].IsEnglish && japaneseStart == start;
        // 前の文脈の点数: 英文の続きなら +2、英語なら +1、日本語なら -1、分からなければ 0。
        int BeforeScore(int start) => start == 0 && englishSentence ? 2 : Score(PrecededByEnglish(start));
        var i = 0;
        while (i < n)
        {
            var found = -1;
            // 英文の中の記号 (, . ! ? -) は読点・句点にせず半角のまま。日本語の文の中の英単語の後 (今日はgoogle、) は日本語の記号。
            // (かな入力では 、。 も かなのキーなので対象外)
            if (!kanaInput && IsAsciiSymbol(units[i]) && PrecededByEnglish(i) == true && segments.All(s => s.IsEnglish)) found = i + 1;
            if (!kanaInput && found < 0 && level != DetectionLevel.Manual) found = CapitalizedWordEnd(units, i, pending, final);
            if (!kanaInput && found < 0 && level != DetectionLevel.Manual) found = HyphenatedWordEnd(units, i, pending);
            for (var j = n; j > i && found < 0; j--)
            {
                // 区間の後ろ: 末尾まで打っているならキャレットの後ろの文字、途中なら続きの日本語。
                var after = j == n ? followingEnglish : false;
                var english = kanaInput
                    ? IsEnglishSpanKana(Raw(units, i, j), Kana(units, i, j), atEnd: j == n, BeforeScore(i), after, level, final)
                    : IsEnglishSpan(Raw(units, i, j) + (j == n ? pending : ""), atEnd: j == n, BeforeScore(i), after, startOfInput: i == 0, level, final,
                        unreadable: HasUnreadable(units, i, j));
                if (english)
                {
                    found = j;
                    break;
                }
            }
            // 途中で終わる英語の区間 (te + al… の teal) より、少し後ろから末尾まで続く長い英単語 (alcoholic) があれば、そちらを取る
            // (sometealcoholic → 染めて + alcoholic。teal を取ると残りの coholic がローマ字になってしまう)。
            if (found > i && found < n && !kanaInput && !IsAsciiSymbol(units[i]))
            {
                for (var k = i + 1; k < found; k++)
                {
                    if (IsLongEnglishWord(Raw(units, k, n) + pending) && n - k > found - i)
                    {
                        found = -1;
                        break;
                    }
                }
            }
            if (found < 0)
            {
                i++;
                continue;
            }
            if (i > japaneseStart) segments.Add(Japanese(units, japaneseStart, i, ""));
            segments.Add(new CompositionSegment(true, "", Raw(units, i, found) + (found == n ? pending : "")));
            i = found;
            japaneseStart = found;
            if (found == n) return segments;
        }

        // Shift を押して打った入力途中の子音 (W, K) は大文字のまま英字で見せる (かなの読み途中として小文字にしない)。
        if (pending.Length > 0 && char.IsAsciiLetterUpper(pending[0]))
        {
            if (japaneseStart < n) segments.Add(Japanese(units, japaneseStart, n, ""));
            segments.Add(new CompositionSegment(true, "", pending));
            return segments;
        }
        if (japaneseStart < n || pending.Length > 0 || segments.Count == 0)
        {
            segments.Add(Japanese(units, japaneseStart, n, pending));
        }
        return segments;
    }

    /// <summary>
    /// 英語とも日本語とも読める語か (i, sushi, make, repo): 英単語で、ローマ字としても最後まで読める (母音か ん で終わる)。
    /// 確定した後で前後の文脈と食い違ったら、確定し直す対象になる。
    /// </summary>
    public bool IsAmbiguousWord(string raw)
    {
        if (raw.Length == 0 || !raw.All(char.IsAsciiLetter) || raw.Any(char.IsAsciiLetterUpper)) return false;
        var lower = raw.ToLowerInvariant();
        if (!_english.Words.ContainsWord(lower) || _proper.Contains(lower)) return false;
        var analysis = _romaji.Analyze(lower);
        return analysis.IsValid && analysis.Partial is "" or "n";
    }

    /// <summary>
    /// 確実に英語の語か (want, google, Tokyo): ローマ字として読めない・子音で終わる英単語・固有名詞・大文字で始まる。
    /// 前後の文脈にかかわらず英語なので、直前に確定した語を確定し直す根拠にできる。
    /// </summary>
    public bool IsDefinitelyEnglish(string raw)
    {
        if (raw.Length == 0 || !raw.All(char.IsAsciiLetter)) return false;
        if (char.IsAsciiLetterUpper(raw[0])) return true;
        var lower = raw.ToLowerInvariant();
        if (_proper.Contains(lower)) return true;
        var analysis = _romaji.Analyze(lower);
        if (!analysis.IsValid) return _english.Words.ContainsWord(lower) || _english.IsPrefix(lower) || lower.Length >= 4;
        // 確定するときに呼ぶので、語は打ち終わっている (it が itai の打ちかけかは気にしない)。
        var word = _english.Words.ContainsWord(lower) || IsSpellWord(lower);
        return word && analysis.Partial.Length > 0 && analysis.Partial != "n";
    }

    /// <summary>
    /// Space を押した時点で、かなにならない子音が残る英単語か (my, by, meeting)。日本語として変換しても子音が残るだけなので、
    /// 英語として確定して空白を入れる。
    /// </summary>
    public bool IsEnglishAtWordEnd(string raw, DetectionLevel level)
    {
        if (level == DetectionLevel.Manual || raw.Length < 2 || !raw.All(char.IsAsciiLetter)) return false;
        var lower = raw.ToLowerInvariant();
        var analysis = _romaji.Analyze(lower);
        if (!analysis.IsValid || analysis.Partial.Length == 0 || analysis.Partial is "n" or "nn") return false;
        return _english.Words.ContainsWord(lower) || IsSpellWord(lower);
    }

    /// <summary>スペルチェッカーが正しいと言う英単語か、よくある打ち間違い (teh、recieve) か。</summary>
    private bool IsSpellWord(string lower) => SpellChecker is { } checker && (checker.IsWord(lower) || checker.AutoCorrection(lower) is not null);

    /// <summary>よくある英語の打ち間違いなら正しい綴り (teh → the)。大文字で始まる語は大文字で始める。</summary>
    public string? EnglishAutoCorrection(string word)
    {
        if (word.Length < 2 || !word.All(char.IsAsciiLetter) || SpellChecker?.AutoCorrection(word.ToLowerInvariant()) is not { } right) return null;
        if (word.All(char.IsAsciiLetterUpper) && word.Length > 1) return right.ToUpperInvariant();
        return char.IsAsciiLetterUpper(word[0]) ? char.ToUpperInvariant(right[0]) + right[1..] : right;
    }

    /// <summary>同梱の英単語の辞書・固有名詞にある語か、ユーザーが英字に直して覚えた語か (ok、github)。スペルチェッカーは使わない。</summary>
    public bool IsListedEnglishWord(string lower) =>
        lower.Length >= 2 && (Memory?.Get(lower) ?? (_english.Words.ContainsWord(lower) || _proper.Contains(lower)));

    /// <summary>
    /// 知っている英単語か (同梱の辞書・固有名詞・ユーザーが英字に直して覚えた語・4 文字以上ならスペルチェッカー)。
    /// python + no の n のように、英単語の最後の n と次の音がくっつくのを防ぐのに使う。
    /// </summary>
    public bool IsKnownEnglishWord(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.Length < 3 || !lower.All(char.IsAsciiLetterLower)) return false;
        if (Memory?.Get(lower) is { } learned) return learned;
        return _english.Words.ContainsWord(lower) || _proper.Contains(lower) || (lower.Length >= 4 && IsSpellWord(lower));
    }

    private static int Score(bool? english) => english switch { true => 1, false => -1, null => 0 };

    private static bool IsAsciiSymbol(CompositionUnit unit) =>
        unit.Raw.Length == 1 && unit.Raw[0] is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(unit.Raw[0]);

    /// <param name="final">打ち終わった (Space・Enter)。末尾の区間でも、英単語の打ちかけ (amaz) は英語の根拠にしない。</param>
    /// <param name="unreadable">区間にローマ字として読めなかった英字がある (zoom + de の m、bug + wo の g)。</param>
    private bool IsEnglishSpan(string span, bool atEnd, int before, bool? after, bool startOfInput, DetectionLevel level, bool final = false, bool unreadable = false)
    {
        // まだ続きを打つかもしれない末尾の区間 (打ちかけの英単語を英語と見てよい)。
        var growing = atEnd && !final;
        if (IsContraction(span)) return level != DetectionLevel.Manual || char.IsAsciiLetterUpper(span[0]);
        if (span.Length == 0 || !span.All(char.IsAsciiLetter)) return false;
        var lower = span.ToLowerInvariant();
        var inDictionary = _english.Words.ContainsWord(lower);
        // Windows のスペルチェッカーの英単語 (meeting, name …)。ローマ字の語 (kore, sore) まで含む緩いものなので、
        // ローマ字として読めない語か、前後の文脈で英語と分かるときだけ使う (同梱の辞書の語より弱い)。
        var conservative = level == DetectionLevel.Conservative;
        // 小書き文字の綴り (mala = まぁ, xtu = っ) で最後まで読める語は、日本語をわざわざ打っている。同梱の辞書の英単語以外は日本語。
        var smallKanaSpelling = !_romaji.Analyze(lower).IsValid && _romaji.AnalyzeFragment(lower) is { IsValid: true, Partial: "" };
        var spellWord = !inDictionary && !smallKanaSpelling && IsSpellWord(lower);
        var exact = inDictionary || (spellWord && !_romaji.Analyze(lower).IsValid);
        var prefix = growing && lower.Length >= 4 && !conservative && !smallKanaSpelling && _english.IsPrefix(lower);

        // 大文字で始まる語 (Shift を押して打った) は固有名詞や英文。1 文字 (I) でも、末尾まで打っている途中でも英語。
        // 手動でもこれだけは英語にする (Shift を押したのはユーザーの明示的な指定)。
        if (char.IsAsciiLetterUpper(span[0]) && (exact || prefix || atEnd)) return true;
        if (level == DetectionLevel.Manual) return false;
        // ユーザーが英字 / かなに直して覚えた語
        if (Memory?.Get(lower) is { } learned) return learned;
        // c 行の綴りで読める語 (care = かれ、can = かん) が日本語の途中にあるなら、日本語を打っている (fucarete → ふかれて、shoucanshi → しょうかんし)。
        // 入力全体がその語だけのときは英語。
        if (!(startOfInput && atEnd))
        {
            if (lower.Contains('c') && _romaji.Analyze(RomajiDetector.ReadCRow(lower)) is { IsValid: true, Partial: "" or "n" }) return false;
            // v 行 (va = ゔぁ): 辞書の英単語 (video) でなければ日本語 (vanpaia → ゔぁんぱいあ → ヴァンパイア)。
            if (lower.Contains('v') && !lower.Contains('l') && !lower.Contains('x') && !inDictionary && !_proper.Contains(lower) &&
                _romaji.AnalyzeFragment(lower) is { IsValid: true, Partial: "" or "n" })
            {
                return false;
            }
        }
        // 1 文字は英文の中の a / i だけ。
        if (span.Length < 2 && !(lower is "a" or "i" && before >= 2)) return false;

        // 英語の固有名詞 (amazon, adobe, netflix) は、ローマ字として読めても英語。日本語の語と同じ綴りなら除く。
        // 短い名前 (ben, tom) の偶然の一致 (にほんごの|ben|きょう) を避けるため 4 文字以上。文の途中の区間なら
        // 5 文字以上 (きょうは|amazon|で) か、ローマ字として読めないもの。末尾の 4 文字の語は、日本語のすぐ後ろでなければ
        // (ある程度は の teido|ha を tei|doha = Doha にしない)。慎重なら、ローマ字として読めないものだけ。
        if (lower.Length >= 4 && _proper.Contains(lower) && !_japanese.Words.ContainsWord(lower) &&
            (conservative ? !_romaji.Analyze(lower).IsValid : (atEnd && before >= 0) || lower.Length >= 5 || !_romaji.Analyze(lower).IsValid))
        {
            return true;
        }
        if (growing && lower.Length >= 4 && !conservative && !smallKanaSpelling && _proper.HasPrefix(lower) && !_japanese.IsPrefix(lower)) return true;

        // 英単語で、ローマ字として読めない英字を含む (zoom + でかいぎ → m が読めない)。日本語の文の途中でも英語。
        // 日本語の後ろで、助詞 + 読めない英字 (の + ts: jissainotsyu) は、英単語 (not) ではなく 助詞 + 英字。
        if (unreadable && before < 0 && Detection.DictionaryDetector.StartsWithParticle(lower) is { } particle && lower.Length - particle.Length <= 2) return false;
        // 同梱の辞書の英単語で、ローマ字としては促音 (っ) を使わないと読めない語 (issue = いっすえ, apple) は英語。
        // 日本語の語 (の先頭) なら除く。
        if (inDictionary && lower.Length >= 4 && _romaji.Analyze(lower) is { IsValid: true, Sokuon: > 0 } && !_japanese.IsPrefix(lower)) return true;
        // 2 文字でも、同梱の辞書の語で読めない英字がある (ok + notasuku の k) なら英語。
        if (unreadable && (exact || spellWord) && (lower.Length >= 3 || inDictionary)) return true;

        // 英語とも日本語とも読める語 (sushi, repo, make) は前後の両方で決める。前が英語なら +1・日本語なら -1、
        // 後ろも同じように数え、合計が必要な点数に届けば英語 (どちらも分からない・食い違うときは日本語)。
        // 入力の先頭で末尾まで打っている途中なら英単語の先頭でも数える (英文の続きを打っている途中を英字で見せる)。
        // 変換ボックス内の英語区間の続きは、偶然の一致 (google + de) を避けるため 3 文字以上の英単語そのものだけ。
        // 助詞などと同じ 2 文字の語 (no, to, ga) は、前後の両方が英語のときだけ (標準)。
        var ambiguous = level switch
        {
            // 積極的でも、助詞と同じ形の 2 文字 (ni, ga) は英単語の先頭というだけでは英語にしない。
            DetectionLevel.Aggressive => startOfInput && atEnd ? exact || spellWord || (!final && lower.Length >= 3 && _english.IsPrefix(lower)) : exact || spellWord,
            DetectionLevel.Conservative => (exact || spellWord) && (lower.Length >= 3 || before >= 2),
            _ => startOfInput && atEnd ? exact || spellWord || lower.Length == 1 || (!final && !smallKanaSpelling && _english.IsPrefix(lower)) : (exact || spellWord) && lower.Length >= 3,
        };
        var needed = level switch
        {
            DetectionLevel.Aggressive => 1,
            DetectionLevel.Conservative => 2,
            // 助詞と同じ形の 2 文字の語 (no, to, ga) は両側が必要。子音で終わる語 (is, at, my) は日本語の語にならないので片側でよい。
            _ => lower.Length <= 2 && _romaji.Analyze(lower) is { IsValid: true, Partial: "" or "n" } ? 2 : 1,
        };
        // スペルチェッカーだけが知っている、最後までローマ字として読める語 (shite, kore) は、前の英単語 1 つ (push) では足りない
        // (pushshite → pushして)。英文の続き (+2) か、前後の両方が英語のときだけ。
        if (spellWord && !inDictionary && _romaji.Analyze(lower) is { IsValid: true, Partial: "" }) needed = Math.Max(needed, 2);
        // 前が英文でも、後ろが日本語なら英文の強さは数えない (I love |sushi| が好き → 食い違うので日本語)。
        if (ambiguous && (after == false ? Math.Min(before, 1) : before) + Score(after) >= needed) return true;
        var analysis = _romaji.Analyze(lower);
        if (!exact && !prefix && !spellWord) return false;

        if (!analysis.IsValid)
        {
            if (!exact && !prefix) return false;
            if (!exact && smallKanaSpelling) return false;
            // 日本語のすぐ後ろの 2 文字の語で、変換ボックスでは読める綴り (こ + we = こうぇ、wi = うぃ) は日本語。
            if (smallKanaSpelling && lower.Length <= 2 && before < 0) return false;
            // ローマ字として読めない英単語。途中の区間は 3 文字以上だけ (短い語の偶然の一致を避ける)。
            return (atEnd && (exact || !conservative)) || span.Length >= 3;
        }
        // ローマ字として読めても、末尾が子音の英単語 (git, zoom, about) で、日本語の語の途中でもないなら英語。
        // スペルチェッカーだけが知っている語 (meeting, my) は、前が日本語でないときだけ。
        // 打ち終わっていれば、日本語の語の打ちかけ (it → itai) かどうかは気にしなくてよい。
        var notJapanesePrefix = final || !_japanese.IsPrefix(lower);
        if (spellWord && atEnd && before >= 0 && analysis.Partial.Length > 0 && analysis.Partial != "n" && notJapanesePrefix) return true;
        return atEnd && exact && (!conservative || lower.Length >= 3) && analysis.Partial.Length > 0 && analysis.Partial != "n" && notJapanesePrefix;
    }

    /// <summary>
    /// かな入力 (JIS) の区間が英語か。打ったキーの英字 (Raw) が英単語で、かなとしては日本語の語にならないなら英語。
    /// かなとしても日本語の語 (の先頭) になるなら、ローマ字入力の「英語とも日本語とも読める語」と同じく前後の文脈で決める。
    /// </summary>
    private bool IsEnglishSpanKana(string span, string kana, bool atEnd, int before, bool? after, DetectionLevel level, bool final = false)
    {
        if (span.Length == 0 || !span.All(char.IsAsciiLetter)) return false;
        var lower = span.ToLowerInvariant();
        var inDictionary = _english.Words.ContainsWord(lower) || (lower.Length >= 4 && _proper.Contains(lower));
        // キー列が偶然スペルチェッカーの語になることがあるので、スペルチェッカーの語は 4 文字以上だけ。
        var word = inDictionary || (lower.Length >= 4 && IsSpellWord(lower));
        var prefix = atEnd && !final && level == DetectionLevel.Aggressive && lower.Length >= 4 && _english.IsPrefix(lower);

        // Shift を押して打った大文字で始まる語は英語 (手動でも)。
        if (char.IsAsciiLetterUpper(span[0]) && (word || prefix || atEnd)) return true;
        if (level == DetectionLevel.Manual) return false;
        if (Memory?.Get(lower) is { } learned) return learned;
        if (!(word || prefix) || lower.Length < 2) return false;

        var japanese = _kana?.IsJapaneseWordOrPrefix(kana) == true;
        var context = (after == false ? Math.Min(before, 1) : before) + Score(after);
        if (context >= (level == DetectionLevel.Conservative ? 2 : 1)) return true;
        if (japanese || context < 0) return false;
        var minimum = level switch { DetectionLevel.Aggressive => 2, DetectionLevel.Conservative => 4, _ => 3 };
        return lower.Length >= minimum;
    }

    /// <summary>
    /// 大文字で始まる語 (Shift を押して打った) の後ろに日本語が続いているなら、その語の終わり (単位の位置)。無ければ -1。
    /// 大文字で始まる区間は末尾まで英語になるので、そのままでは AutoIMEnotesuto → 全部英字 になってしまう。
    /// 語は、知っている英単語 (Github) か、大文字で終わる語 (AutoIME, OK, NHK)。後ろは 3 文字以上の小文字で、ローマ字として読めるもの。
    /// </summary>
    private int CapitalizedWordEnd(IReadOnlyList<CompositionUnit> units, int start, string pending, bool final)
    {
        var n = units.Count;
        var whole = Raw(units, start, n) + pending;
        if (whole.Length == 0 || !char.IsAsciiLetterUpper(whole[0])) return -1;
        // 全体が英単語・固有名詞 (Tokyo, Github) なら区切らない。
        if (IsKnownCapitalizedWord(whole)) return -1;
        for (var k = n - 1; k > start; k--)
        {
            var head = Raw(units, start, k);
            if (!head.All(char.IsAsciiLetter)) continue;
            // 後ろは小文字のローマ字 (長音の - を含んでもよい: TSyu-za- の yu-za-)。
            var rest = Raw(units, k, n) + pending;
            if (rest.Length < 3 || !rest.All(c => char.IsAsciiLetterLower(c) || c == '-') || !char.IsAsciiLetterLower(rest[0])) continue;
            var analysis = _romaji.AnalyzeFragment(rest.Replace("-", ""));
            if (!analysis.IsValid || (final && analysis.Partial.Length > 0 && analysis.Partial != "n")) continue;
            if (head.Length >= 2 && char.IsAsciiLetterUpper(head[^1]) || head.Length >= 3 && IsKnownCapitalizedWord(head)) return k;
        }
        return -1;
    }

    // - を付けて使う英語の接頭辞 (e-mail、re-do、co-op、x-ray)。接頭辞 + - + 3 文字以上の英単語なら英語。
    // 1 文字の母音 (o-bun = オーブン) は日本語の長音とまぎらわしいので e と x だけ。
    private static readonly HashSet<string> HyphenPrefixes = ["e", "x", "re", "co", "ex", "non", "anti", "semi", "multi", "pre", "sub", "post", "mid", "self", "well"];

    // 接頭辞の規則では拾えない、- の入ったよく使う英単語 (後ろが 2 文字以下など)
    private static readonly HashSet<string> HyphenatedWords = ["co-op", "re-do", "x-ray", "t-shirt", "wi-fi", "hi-fi", "e-book", "e-sports", "k-pop", "j-pop", "j-rock", "a-z", "u-turn", "check-in", "log-in", "sign-in", "add-on", "plug-in", "built-in", "follow-up", "set-up", "pop-up", "drop-down"];

    /// <summary>- を付けて使う英語の接頭辞か (e、re、co …)。英数状態で、- の後を見てから英語か決めるのに使う。</summary>
    public static bool IsHyphenPrefix(string lower) => HyphenPrefixes.Contains(lower);

    /// <summary>- の入った英単語か (e-mail、co-op、re-do、x-ray)。</summary>
    public bool IsHyphenatedEnglishWord(string lower)
    {
        if (HyphenatedWords.Contains(lower)) return true;
        var dash = lower.IndexOf('-');
        if (dash <= 0 || lower.IndexOf('-', dash + 1) >= 0) return false;
        var rest = lower[(dash + 1)..];
        return HyphenPrefixes.Contains(lower[..dash]) && rest.Length >= 3 && rest.All(char.IsAsciiLetterLower) && IsKnownEnglishWord(rest);
    }

    /// <summary>
    /// start から始まる - の入った英単語 (e-mail、co-op) の終わり。無ければ -1。
    /// 後ろに日本語が続いてもよい (e-mail|de) ので、- の後ろは長い方から英単語になる所を探す。
    /// </summary>
    private int HyphenatedWordEnd(IReadOnlyList<CompositionUnit> units, int start, string pending)
    {
        var n = units.Count;
        // 語の途中からは探さない
        if (start > 0 && units[start - 1].Raw.All(char.IsAsciiLetter) && units[start - 1].Raw.Length > 0) return -1;
        var dash = start;
        while (dash < n && units[dash].Raw.Length > 0 && units[dash].Raw.All(char.IsAsciiLetter)) dash++;
        if (dash == start || dash >= n || units[dash].Raw != "-") return -1;
        for (var end = n; end > dash + 1; end--)
        {
            var word = (Raw(units, start, end) + (end == n ? pending : "")).ToLowerInvariant();
            if (!word[(word.IndexOf('-') + 1)..].All(char.IsAsciiLetterLower)) continue;
            if (IsHyphenatedEnglishWord(word)) return end;
        }
        return -1;
    }

    /// <summary>英語の短縮形 (don't, it's, I'm, you're, we'll, can't)。' の前が英単語か n't の形。</summary>
    private bool IsContraction(string span)
    {
        var apostrophe = span.IndexOf('\'');
        if (apostrophe <= 0 || apostrophe != span.LastIndexOf('\'')) return false;
        var stem = span[..apostrophe].ToLowerInvariant();
        var suffix = span[(apostrophe + 1)..].ToLowerInvariant();
        if (!stem.All(char.IsAsciiLetterLower) || suffix is not ("t" or "s" or "re" or "ve" or "ll" or "d" or "m")) return false;
        if (suffix == "t") return stem.Length >= 2 && stem[^1] == 'n';
        return stem == "i" || _english.Words.ContainsWord(stem) || _proper.Contains(stem);
    }

    /// <summary>ローマ字として読めない、5 文字以上の英単語 (alcoholic, pressure)。</summary>
    private bool IsLongEnglishWord(string raw)
    {
        if (raw.Length < 5 || !raw.All(char.IsAsciiLetter)) return false;
        var lower = raw.ToLowerInvariant();
        if (_romaji.Analyze(lower).IsValid) return false;
        return _english.Words.ContainsWord(lower) || _proper.Contains(lower) || IsSpellWord(lower);
    }

    private bool IsKnownCapitalizedWord(string word)
    {
        var lower = word.ToLowerInvariant();
        return Memory?.Get(lower) == true || _english.Words.ContainsWord(lower) || _proper.Contains(lower);
    }

    /// <summary>最初の 3 文字以内でローマ字として読めなくなる、4 文字以上の語 (日本語の打ち間違いでもないもの)。</summary>
    private bool IsUnknownEnglishWord(string raw)
    {
        if (raw.Length < 4 || !raw.All(char.IsAsciiLetter)) return false;
        var lower = raw.ToLowerInvariant();
        var analysis = _romaji.Analyze(lower);
        if (analysis.IsValid) return false;
        // 小書き文字などの綴り (kaxnji, ulo) まで含めれば読めるなら、日本語を打っている。
        if (_romaji.AnalyzeFragment(lower).IsValid) return false;
        var firstInvalid = analysis.Tokens.Sum(t => t.Romaji.Length);
        if (firstInvalid >= 3) return false;
        // 読めない文字の後ろが普通のローマ字なら、英字 1 文字 + 日本語 (sだけが, Xがわかる) を打っている。
        var rest = lower[(firstInvalid + 1)..];
        if (rest.Length >= 3 && _romaji.AnalyzeFragment(rest).IsValid) return false;
        var typo = new List<Contribution>();
        _typo.Evaluate(lower, typo);
        return typo.Count == 0;
    }

    private static CompositionSegment Japanese(IReadOnlyList<CompositionUnit> units, int start, int end, string pending)
    {
        var kana = string.Concat(Enumerable.Range(start, end - start).Select(k => units[k].Kana));
        return new CompositionSegment(false, kana, Raw(units, start, end) + pending);
    }

    /// <summary>単位 [start, end) に、ローマ字として読めなかった英字 (かなにならなかった 1 文字) があるか。</summary>
    private static bool HasUnreadable(IReadOnlyList<CompositionUnit> units, int start, int end)
    {
        for (var k = start; k < end; k++)
        {
            if (units[k] is { Raw.Length: 1 } unit && unit.Kana == unit.Raw && char.IsAsciiLetter(unit.Raw[0])) return true;
        }
        return false;
    }

    private static string Kana(IReadOnlyList<CompositionUnit> units, int start, int end) =>
        string.Concat(Enumerable.Range(start, end - start).Select(k => units[k].Kana));

    private static string Raw(IReadOnlyList<CompositionUnit> units, int start, int end) =>
        string.Concat(Enumerable.Range(start, end - start).Select(k => units[k].Raw));
}
