// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Text.Json;

namespace Meltype.Composition;

/// <summary>
/// 英語とも日本語とも読める語 (api, sushi …) を、ユーザーが自分で英字 / かなに直したときに覚えておく。
/// 次からその語は、自動の判定より覚えた方を優先する (api と打って F10 で英字にして確定 → 次から api は英字)。
/// %LOCALAPPDATA%\Meltype\languages.json に「打った英字 → 英語か」だけを保存する。
/// </summary>
public sealed class LanguageMemory
{
    private const int MaxEntries = 3000;
    private readonly string? _path;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private sealed class Entry
    {
        public bool English { get; set; }
        public DateTime Used { get; set; }

        /// <summary>同じ向きに直した回数 (前の版で保存したものは 0 = 1 回)。</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// ローマ字としてよく使う日本語になる語か (kyouha = 今日は、sushi = すし)。こういう語は、一度英字にして確定しただけでは
    /// 英語として覚えない (2 回で覚える)。間違えて一度確定しただけで、ふつうの日本語がずっと英字になってしまうため。
    /// </summary>
    public Func<string, bool>? IsCommonJapanese { get; set; }

    public LanguageMemory(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path));
            if (loaded is not null) foreach (var (word, entry) in loaded) _entries[word] = entry;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Warn($"英語 / 日本語の学習データを読めませんでした: {ex.Message}");
        }
    }

    public int Count => _entries.Count;

    /// <summary>覚えている語なら英語か (true) 日本語か (false)。覚えていなければ null。word は小文字の英字。</summary>
    public bool? Get(string word)
    {
        if (!_entries.TryGetValue(word, out var entry)) return null;
        if (entry.English && Math.Max(1, entry.Count) < 2 && IsCommonJapanese?.Invoke(word) == true) return null;
        return entry.English;
    }

    /// <summary>ユーザーが英字 / かなに直した語を覚える (2 文字以上の英字だけ)。</summary>
    public void Remember(string word, bool english)
    {
        word = word.ToLowerInvariant();
        if (word.Length < 2 || !word.All(char.IsAsciiLetterLower)) return;
        if (_entries.TryGetValue(word, out var old) && old.English == english)
        {
            old.Used = DateTime.UtcNow;
            old.Count = Math.Max(1, old.Count) + 1;
            if (old.Count == 2 && english && IsCommonJapanese?.Invoke(word) == true) Diagnostics.Log.Decision($"「{word}」は次から英語にします (2 回目の学習)。");
        }
        else
        {
            _entries[word] = new Entry { English = english, Used = DateTime.UtcNow, Count = 1 };
            Diagnostics.Log.Decision(english && IsCommonJapanese?.Invoke(word) == true
                ? $"「{word}」はよく使う日本語の読みなので、もう一度英字にして確定したら英語にします (学習)。"
                : $"「{word}」は次から{(english ? "英語" : "日本語")}にします (学習)。");
        }
        if (_entries.Count > MaxEntries)
        {
            foreach (var key in _entries.OrderBy(e => e.Value.Used).Take(_entries.Count - MaxEntries * 9 / 10).Select(e => e.Key).ToList())
            {
                _entries.Remove(key);
            }
        }
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Save()
    {
        if (_path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Warn($"英語 / 日本語の学習データを保存できませんでした: {ex.Message}");
        }
    }
}
