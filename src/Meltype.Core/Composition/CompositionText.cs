// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Text;
using Meltype.Config;

namespace Meltype.Composition;

/// <summary>変換ボックスの表示形式。Auto 以外は F6/F7/F9/F10 などでユーザーが明示的に選んだもの。</summary>
public enum DisplayMode { Auto, Hiragana, Katakana, FullWidthAlphanumeric, HalfWidthAlphanumeric }

/// <summary>
/// 変換ボックス内の 1 単位。ローマ字 1 音 (きょ, っ, ん …)・ローマ字として読めなかった英字 1 文字・記号 1 文字のいずれか。
/// Raw は実際に打った文字 (英語として表示するときに使う)。
/// </summary>
public readonly record struct CompositionUnit(string Kana, string Raw);

/// <summary>表示上のひとまとまり。英語と判定した区間は英字のまま、それ以外は日本語 (かな/漢字)。</summary>
public readonly record struct CompositionSegment(bool IsEnglish, string Kana, string Raw);

/// <summary>
/// 未確定の入力。打ったそばからローマ字をかな 1 音ずつの単位にしていき、まだ音にならない子音
/// (k, ky, n …) だけを Pending に残す。BackSpace は 1 音ずつ消える。
/// 英単語の判定は区間ごとに行うので、「きょうは google」のように日本語の後ろの英単語だけが英字になる。
/// </summary>
public sealed class CompositionText
{
    private readonly List<CompositionUnit> _units = [];
    private readonly StringBuilder _pending = new();
    private readonly CompositionDetector _detector;

    public CompositionText(CompositionDetector detector) => _detector = detector;

    public IReadOnlyList<CompositionUnit> Units => _units;
    public string Pending => _pending.ToString();
    public bool IsEmpty => _units.Count == 0 && _pending.Length == 0;
    public DisplayMode Mode { get; set; } = DisplayMode.Auto;

    /// <summary>自動判定の強さ (設定)。</summary>
    public Func<DetectionLevel> Level { get; set; } = () => DetectionLevel.Balanced;

    /// <summary>この入力だけ判定の強さを変える (手動のときに Tab で提案を受け入れる)。確定・取り消しで戻る。</summary>
    public DetectionLevel? LevelOverride { get; set; }

    private DetectionLevel EffectiveLevel => LevelOverride ?? Level();

    /// <summary>かな入力 (JIS) か。かな入力では <see cref="AppendKana"/> で 1 キー 1 文字ずつ入れる。</summary>
    public bool KanaInput { get; set; }

    /// <summary>
    /// かな入力の 1 キー。raw はそのキーの英字 (英単語の判定と、英語として見せるときに使う)。
    /// 濁点・半濁点は直前のかなに付ける (か + ゛ → が)。
    /// </summary>
    public void AppendKana(char raw, char kana)
    {
        if (kana is '゛' or '゜' && _units.Count > 0 && _units[^1].Kana.Length == 1 &&
            Detection.KanaDetector.Combine(_units[^1].Kana[0], kana) is { } combined)
        {
            var last = _units[^1];
            _units[^1] = new CompositionUnit(combined.ToString(), last.Raw + raw);
            return;
        }
        _units.Add(new CompositionUnit(kana.ToString(), raw.ToString()));
    }

    /// <summary>打った文字そのもの。</summary>
    public string Raw => string.Concat(_units.Select(u => u.Raw)) + _pending;

    public void Append(char c)
    {
        if (char.IsAsciiLetter(c))
        {
            SplitEnglishFinalN(c);
            // 直前の「ローマ字として読めなかった英字」は、次の文字と合わせると読めることがある
            // (test の t を消して u を打つ → s + u = す)。入力途中の子音に戻して読み直す。
            var pulled = new StringBuilder();
            while (_units.Count > 0 && _units[^1] is { Raw.Length: 1 } last && last.Kana == last.Raw && char.IsAsciiLetter(last.Raw[0]))
            {
                pulled.Insert(0, last.Raw);
                _units.RemoveAt(_units.Count - 1);
            }
            _pending.Insert(0, pulled.ToString());
            _pending.Append(c);
            Normalize(final: false);
            return;
        }
        if (_pending.Length > 0 && char.ToLowerInvariant(_pending[^1]) == 'z' && ZSymbol(c) is { } z)
        {
            var raw = _pending[^1].ToString() + c;
            _pending.Length--;
            Normalize(final: true);
            _units.Add(new CompositionUnit(z.ToString(), raw));
            return;
        }
        // 記号・数字の前で、途中の n は ん に、読めない子音は英字のまま確定させる。
        Normalize(final: true);
        // 、 や 。 を 3 つ続けたら ... にする (、、、 → ...)。
        if (c is ',' or '.')
        {
            var run = 0;
            while (run < _units.Count && _units[^(run + 1)].Raw is "," or ".") run++;
            if (run >= 2)
            {
                for (var k = _units.Count - run; k < _units.Count; k++) _units[k] = _units[k] with { Kana = "." };
                _units.Add(new CompositionUnit(".", c.ToString()));
                return;
            }
        }
        _units.Add(new CompositionUnit(Symbol(c).ToString(), c.ToString()));
    }

    /// <summary>
    /// 英単語の最後の n と、続けて打った n + 母音 (の・な …) が「nn → ん」とまとまってしまうのを防ぐ
    /// (python + no → pythonno → python + ん + お ではなく python + の)。
    /// 直前の単位が nn でできた ん で、その前の英字と 1 つ目の n で知っている英単語になり、今打ったのが母音か y なら、
    /// ん を 1 つ目の n だけにして、2 つ目の n を今打った文字とつなげる。
    /// </summary>
    private void SplitEnglishFinalN(char c)
    {
        if (_pending.Length > 0 || _units.Count < 2 || char.ToLowerInvariant(c) is not ('a' or 'i' or 'u' or 'e' or 'o' or 'y')) return;
        if (_units[^1] is not { Kana: "ん" } last || !last.Raw.Equals("nn", StringComparison.OrdinalIgnoreCase)) return;
        // 直前の英字の並び (英字だけの単位) の後ろの部分 + n が英単語か (きょうは + python の python)。
        var letters = new StringBuilder();
        for (var i = _units.Count - 2; i >= 0 && _units[i].Raw.All(char.IsAsciiLetter); i--) letters.Insert(0, _units[i].Raw);
        var run = letters.ToString() + last.Raw[0];
        for (var start = 0; start <= run.Length - 3; start++)
        {
            if (!_detector.IsKnownEnglishWord(run[start..])) continue;
            _units[^1] = new CompositionUnit("ん", last.Raw[..1]);
            _pending.Append(last.Raw[1]);
            return;
        }
    }

    /// <summary>1 音 (または 1 文字) 消す。入力途中の子音があればそれを 1 文字消す。</summary>
    public void RemoveLast()
    {
        if (_pending.Length > 0) _pending.Length--;
        else if (_units.Count > 0)
        {
            var englishTail = Mode is DisplayMode.HalfWidthAlphanumeric or DisplayMode.FullWidthAlphanumeric ||
                              (Mode == DisplayMode.Auto && Segments() is { Count: > 0 } segments && segments[^1].IsEnglish);
            var last = _units[^1];
            _units.RemoveAt(_units.Count - 1);
            // 英字として見せている部分は 1 文字ずつ消す (google の "le" のように 1 音にまとまった単位を丸ごと消さない)。
            if (englishTail && last.Raw.Length > 1 && !KanaInput)
            {
                _pending.Append(last.Raw[..^1]);
                Normalize(final: false);
            }
        }
        if (IsEmpty)
        {
            Mode = DisplayMode.Auto;
            LevelOverride = null;
        }
    }

    public void Clear()
    {
        _units.Clear();
        _pending.Clear();
        Mode = DisplayMode.Auto;
        LevelOverride = null;
    }

    /// <summary>Pending のうち、音として確定した部分を単位に移す。</summary>
    private void Normalize(bool final)
    {
        var romaji = _detector.Romaji;
        while (_pending.Length > 0)
        {
            var original = _pending.ToString();
            var analysis = romaji.AnalyzeFragment(original.ToLowerInvariant());
            var position = 0;
            var tokens = analysis.Tokens;
            for (var i = 0; i < tokens.Count; i++)
            {
                var length = tokens[i].Romaji.Length;
                _units.Add(new CompositionUnit(tokens[i].Kana, original.Substring(position, length)));
                position += length;
            }

            if (analysis.IsValid)
            {
                var rest = original[position..];
                _pending.Clear();
                if (final && rest.Length > 0)
                {
                    // 確定時: 残った n / nn は ん、それ以外の子音は英字のまま。
                    if (rest.ToLowerInvariant() is "n" or "nn") _units.Add(new CompositionUnit("ん", rest));
                    else foreach (var c in rest) _units.Add(new CompositionUnit(c.ToString(), c.ToString()));
                }
                else
                {
                    _pending.Append(rest);
                }
                return;
            }

            // ローマ字として読めない文字 (google の l など) は英字 1 文字の単位にして、続きを読み直す。
            _units.Add(new CompositionUnit(original[position].ToString(), original[position].ToString()));
            _pending.Clear();
            _pending.Append(original[(position + 1)..]);
        }
    }

    /// <summary>数字だけ (と . , : - /) の入力か。数字は半角のまま、Space で確定して空白を入れる (英語と同じ扱い)。</summary>
    public bool IsNumeric =>
        !KanaInput && _pending.Length == 0 && _units.Count > 0 && _units.Any(u => u.Raw.Any(char.IsAsciiDigit)) &&
        _units.All(u => u.Raw.All(c => char.IsAsciiDigit(c) || c is '.' or ',' or ':' or '-' or '/'));

    /// <summary>今の表示が英字 (か数字) だけか (Space で「確定して空白」にするかどうか)。</summary>
    public bool IsAlphanumeric => IsAlphanumericAt(final: false);

    /// <summary>今の表示が英字 (か数字) だけか。final なら打ち終わったとみなして判定する (Space・Enter のとき)。</summary>
    public bool IsAlphanumericAt(bool final) => Mode switch
    {
        DisplayMode.HalfWidthAlphanumeric or DisplayMode.FullWidthAlphanumeric => true,
        DisplayMode.Auto => IsNumeric || Segments(final).All(s => s.IsEnglish),
        _ => false,
    };

    /// <summary>自動判定の区間分け (Mode が Auto のときに使う)。</summary>
    /// <param name="final">打ち終わった (Space・Enter で確定・変換する) ときは true。英単語の打ちかけ (amaz → amazon) を英語の根拠にしない。</param>
    public IReadOnlyList<CompositionSegment> Segments(bool final = false) => _detector.Segment(_units, Pending, PrecedingEnglish, FollowingEnglish, EffectiveLevel, PrecedingEnglishSentence, KanaInput, final);

    /// <summary>判定の強さが「手動」のとき、標準の判定なら英字にする部分 (提案)。無ければ null。</summary>
    public string? Suggestion()
    {
        if (Mode != DisplayMode.Auto || EffectiveLevel != DetectionLevel.Manual || IsNumeric) return null;
        var suggested = _detector.Segment(_units, Pending, PrecedingEnglish, FollowingEnglish, DetectionLevel.Balanced, PrecedingEnglishSentence, KanaInput);
        if (!suggested.Any(s => s.IsEnglish) || suggested.SequenceEqual(Segments())) return null;
        return string.Join(" ", suggested.Where(s => s.IsEnglish).Select(s => s.Raw));
    }

    /// <summary>
    /// 入力欄のキャレットの前の確定済みの文字が英語なら true、日本語なら false、分からなければ null。
    /// 英語とも日本語とも読める語 (sushi など) の扱いを、後ろ (<see cref="FollowingEnglish"/>) と合わせて決める。
    /// </summary>
    public bool? PrecedingEnglish { get; set; }

    /// <summary>キャレットの前が英文の途中 ("I want ") か。日本語の文の中の英単語 (GitHub の) より強い英語の根拠。</summary>
    public bool PrecedingEnglishSentence { get; set; }

    /// <summary>キャレットの後ろの文字が英語なら true、日本語なら false、分からなければ null。</summary>
    public bool? FollowingEnglish { get; set; }

    /// <summary>変換用の区間分け。末尾の入力途中の子音は確定扱い (n → ん) にして日本語区間の読みに含める。</summary>
    public IReadOnlyList<CompositionSegment> ConversionSegments()
    {
        var segments = Segments(final: true).ToList();
        if (segments.Count > 0 && !segments[^1].IsEnglish)
        {
            segments[^1] = segments[^1] with { Kana = segments[^1].Kana + PendingText(final: true) };
        }
        return segments;
    }

    /// <summary>
    /// <see cref="ConversionSegments"/> の segmentIndex 番目 (日本語の区間) の読みのうち [start, start + length) に対応する、打った英字
    /// (あぴ → api)。変換の候補に「打ったままの英字」を出すのに使う。読みの区切りが 1 音の区切りと合わないときや、かな入力では null。
    /// </summary>
    public string? RawForReading(int segmentIndex, int start, int length)
    {
        if (KanaInput) return null;
        var segments = Segments(final: true);
        if (segmentIndex < 0 || segmentIndex >= segments.Count) return null;
        // 区間ごとに単位を割り当てる (区間の Raw の長さぶんの単位)。
        var unit = 0;
        for (var s = 0; s < segmentIndex; s++)
        {
            var remaining = segments[s].Raw.Length;
            while (unit < _units.Count && remaining > 0) remaining -= _units[unit++].Raw.Length;
        }
        var pieces = new List<(string Kana, string Raw)>();
        var rawLength = segments[segmentIndex].Raw.Length - (segmentIndex == segments.Count - 1 ? _pending.Length : 0);
        while (unit < _units.Count && rawLength > 0)
        {
            pieces.Add((_units[unit].Kana, _units[unit].Raw));
            rawLength -= _units[unit++].Raw.Length;
        }
        if (segmentIndex == segments.Count - 1 && _pending.Length > 0) pieces.Add((PendingText(final: true), Pending));

        var builder = new StringBuilder();
        var position = 0;
        foreach (var (kana, raw) in pieces)
        {
            var end = position + kana.Length;
            if (end > start && position < start + length)
            {
                // 1 音の途中で区切れているなら、打った英字に対応させられない。
                if (position < start || end > start + length) return null;
                builder.Append(raw);
            }
            position = end;
        }
        return builder.Length > 0 ? builder.ToString() : null;
    }

    /// <param name="final">確定・変換のときは true (語末の n を ん にする)。</param>
    /// <param name="convert">日本語の区間を漢字に変換する関数 (ライブ変換)。null ならかなのまま。</param>
    public string Display(bool final, Func<string, string>? convert = null) => Mode switch
    {
        DisplayMode.HalfWidthAlphanumeric => Raw,
        DisplayMode.FullWidthAlphanumeric => ToFullWidth(Raw),
        DisplayMode.Hiragana => AllKana(final),
        DisplayMode.Katakana => ToKatakana(AllKana(final)),
        _ => IsNumeric ? Raw : RenderSegments(final, convert),
    };

    /// <summary>英語区間は英字のまま、日本語区間はかな (または漢字)。</summary>
    public string RenderSegments(bool final, Func<string, string>? convert)
    {
        var builder = new StringBuilder();
        var segments = Segments(final);
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.IsEnglish)
            {
                builder.Append(segment.Raw);
                continue;
            }
            var isLast = i == segments.Count - 1;
            var kana = segment.Kana;
            var pending = isLast ? PendingText(final) : "";
            if (final && isLast && pending == "ん")
            {
                kana += pending;
                pending = "";
            }
            builder.Append(convert is not null && kana.Length > 0 ? convert(kana) : kana);
            builder.Append(pending);
        }
        return builder.ToString();
    }

    /// <summary>英語判定を無視してすべてかなにしたもの (F6)。</summary>
    public string AllKana(bool final) => string.Concat(_units.Select(u => u.Kana)) + PendingText(final);

    /// <summary>
    /// 日本語の部分の読みに書き間違い (ブレスレッド) があれば、その位置 (単位の範囲) と正しい読みを返す。
    /// 英語として見せている部分や、入力途中の子音は見ない。読みの区切りが単位の区切りと合わないときも null。
    /// </summary>
    public (int Start, int End, Misspelling Misspelling)? FindMisspelling(MisspellingDictionary dictionary)
    {
        if (Mode != DisplayMode.Auto || _units.Count == 0) return null;
        var english = UnitIsEnglish();
        // 日本語の単位が続く範囲ごとに探す。
        var start = 0;
        while (start < _units.Count)
        {
            if (english[start])
            {
                start++;
                continue;
            }
            var end = start;
            while (end < _units.Count && !english[end]) end++;
            var offsets = new List<int>();
            var reading = new StringBuilder();
            for (var i = start; i < end; i++)
            {
                offsets.Add(reading.Length);
                reading.Append(_units[i].Kana);
            }
            offsets.Add(reading.Length);
            // 語末の n (まだ ん になっていない) も ん として読む (しゅみれーしょn)。
            if (end == _units.Count && Pending.ToLowerInvariant() is "n" or "nn")
            {
                reading.Append('ん');
                offsets.Add(reading.Length);
            }
            if (dictionary.Find(reading.ToString()) is { } found)
            {
                var first = offsets.IndexOf(found.Start);
                var last = offsets.IndexOf(found.Start + found.Length);
                if (first >= 0 && last > first) return (start + first, start + last, found);
            }
            start = end;
        }
        return null;
    }

    /// <summary>単位 [start, end) の読みを、正しい読み (カタカナ) に置き換える。end が単位の数を超えるときは語末の n も含む。</summary>
    public void ReplaceReading(int start, int end, string rightKatakana)
    {
        var hiragana = new string(rightKatakana.Select(c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c).ToArray());
        if (end > _units.Count)
        {
            _pending.Clear();
            end = _units.Count;
        }
        _units.RemoveRange(start, end - start);
        // かなの単位にする (英字の Raw を持たないので、英語と判定されることはない)。
        _units.InsertRange(start, hiragana.Select(c => new CompositionUnit(c.ToString(), c.ToString())));
    }

    /// <summary>単位ごとに、英語として見せている区間に入っているか。</summary>
    private bool[] UnitIsEnglish()
    {
        var result = new bool[_units.Count];
        var unit = 0;
        foreach (var segment in Segments())
        {
            var remaining = segment.Raw.Length;
            while (unit < _units.Count && remaining > 0)
            {
                result[unit] = segment.IsEnglish;
                remaining -= _units[unit].Raw.Length;
                unit++;
            }
        }
        return result;
    }

    private string PendingText(bool final)
    {
        var pending = Pending;
        if (pending.Length == 0) return "";
        return _detector.Romaji.ConvertLenient(pending.ToLowerInvariant(), final);
    }

    /// <summary>日本語の中で打った記号 (Microsoft IME と同じく全角)。英語の区間では打ったままの半角で出す。数字は半角のまま。</summary>
    private static char Symbol(char c) => c switch
    {
        '-' => 'ー',
        ',' => '、',
        '.' => '。',
        '[' => '「',
        ']' => '」',
        '~' => '～',
        '\'' => '’',
        '"' => '”',
        // @ はメールアドレス・メンションで使うので、日本語の中でも半角のまま。
        '@' => '@',
        // JIS キーボードの ￥ キー
        '\\' => '￥',
        _ when c is >= '!' and <= '~' && !char.IsAsciiLetterOrDigit(c) => (char)(c + 0xFEE0),
        _ => c,
    };

    /// <summary>全角の記号 (＃ （ ％ ’ ￥) を半角に戻す。かな・漢字・句読点 (、。「」ー) はそのまま。</summary>
    public static string SymbolsToHalfWidth(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                '’' => '\'',
                '”' => '"',
                '￥' => '¥',
                >= '！' and <= '～' when !char.IsLetterOrDigit(chars[i]) => (char)(chars[i] - 0xFEE0),
                _ => chars[i],
            };
        }
        return new string(chars);
    }

    /// <summary>z + 記号 (Microsoft IME と同じ): z/ → ・、z. → …、z, → ‥、z- → ～、z[ → 『、z] → 』。</summary>
    private static char? ZSymbol(char c) => c switch
    {
        '/' => '・',
        '.' => '…',
        ',' => '‥',
        '-' => '～',
        '[' => '『',
        ']' => '』',
        _ => null,
    };

    public static string ToKatakana(string hiragana)
    {
        var chars = hiragana.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= 'ぁ' and <= 'ゖ') chars[i] = (char)(chars[i] + 0x60);
        }
        return new string(chars);
    }

    public static string ToFullWidth(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '!' and <= '~') chars[i] = (char)(chars[i] + 0xFEE0);
            else if (chars[i] == ' ') chars[i] = '　';
        }
        return new string(chars);
    }
}
