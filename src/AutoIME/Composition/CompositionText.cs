using System.Text;
using AutoIME.Config;

namespace AutoIME.Composition;

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
        // 記号・数字の前で、途中の n は ん に、読めない子音は英字のまま確定させる。
        Normalize(final: true);
        _units.Add(new CompositionUnit(Symbol(c).ToString(), c.ToString()));
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
    public bool IsAlphanumeric => Mode switch
    {
        DisplayMode.HalfWidthAlphanumeric or DisplayMode.FullWidthAlphanumeric => true,
        DisplayMode.Auto => IsNumeric || Segments().All(s => s.IsEnglish),
        _ => false,
    };

    /// <summary>自動判定の区間分け (Mode が Auto のときに使う)。</summary>
    public IReadOnlyList<CompositionSegment> Segments() => _detector.Segment(_units, Pending, PrecedingEnglish, FollowingEnglish, EffectiveLevel, PrecedingEnglishSentence, KanaInput);

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
        var segments = Segments().ToList();
        if (segments.Count > 0 && !segments[^1].IsEnglish)
        {
            segments[^1] = segments[^1] with { Kana = segments[^1].Kana + PendingText(final: true) };
        }
        return segments;
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
        var segments = Segments();
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

    private string PendingText(bool final)
    {
        var pending = Pending;
        if (pending.Length == 0) return "";
        return _detector.Romaji.ConvertLenient(pending.ToLowerInvariant(), final);
    }

    private static char Symbol(char c) => c switch
    {
        '-' => 'ー',
        ',' => '、',
        '.' => '。',
        '?' => '？',
        '!' => '！',
        '[' => '「',
        ']' => '」',
        '~' => '～',
        '/' => '・',
        _ => c,
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
