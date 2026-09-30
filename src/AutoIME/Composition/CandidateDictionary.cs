namespace AutoIME.Composition;

/// <summary>
/// 変換候補の補助辞書 (読み → 候補)。Microsoft IME の変換エンジンは最有力の 1 候補しか返さないので、
/// 同音異義語 (はし → 橋/箸/端、とうてん → 当店/読点) はここから出す。
/// 形式は 1 行に「読み 候補 候補 …」。# 以降はコメント。
/// 組み込みの dictionaries/candidates.txt と %LOCALAPPDATA%\AutoIME\dictionaries\candidates.txt を読む。
/// </summary>
public sealed class CandidateDictionary
{
    private readonly Dictionary<string, List<string>> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public static CandidateDictionary Load(string? userDirectory)
    {
        var dictionary = new CandidateDictionary();
        dictionary.AddText(Detection.DictionarySource.ReadEmbedded("candidates.txt"));
        if (userDirectory is not null)
        {
            var path = Path.Combine(userDirectory, "candidates.txt");
            try
            {
                if (File.Exists(path)) dictionary.AddText(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Warn($"ユーザーの候補辞書を読めませんでした: {ex.Message}");
            }
        }
        return dictionary;
    }

    public void AddText(string text)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var parts = line.Split([' ', '\t', '\r', '　'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            Add(parts[0], parts.Skip(1));
        }
    }

    public void Add(string reading, IEnumerable<string> words)
    {
        if (!_entries.TryGetValue(reading, out var list)) _entries[reading] = list = [];
        foreach (var word in words)
        {
            if (!list.Contains(word)) list.Add(word);
        }
    }

    /// <summary>
    /// 文節の読みに対する候補。文節は助詞などを含む (はしを) ので、辞書にある最長の先頭部分を置き換え、
    /// 残りはかなのまま付ける (箸を / 端を)。
    /// </summary>
    public IReadOnlyList<string> Lookup(string reading)
    {
        for (var length = reading.Length; length >= 1; length--)
        {
            if (_entries.TryGetValue(reading[..length], out var words))
            {
                var rest = reading[length..];
                return words.Select(w => w + rest).ToList();
            }
        }
        return [];
    }
}
