// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

namespace AutoIME.Detection;

/// <summary>
/// 日本語辞書 (ローマ字見出し) との前方一致。見出し語から綴りの揺れ
/// (shi/si, tsu/tu, chi/ti, ん の n/nn …) を展開して登録するので、訓令式で打っても一致する。
/// </summary>
public sealed class DictionaryDetector
{
    public WordList Words { get; } = new();

    public DictionaryDetector(IEnumerable<string> canonicalWords, RomajiDetector romaji)
    {
        foreach (var word in canonicalWords)
        {
            foreach (var variant in romaji.SpellingVariants(word)) Words.Add(variant);
        }
    }

    public bool IsPrefix(string letters) => Words.HasPrefix(letters);

    public void Evaluate(string letters, List<Contribution> output)
    {
        if (letters.Length < 3) return;
        if (Words.ContainsWord(letters))
        {
            output.Add(new Contribution("Dictionary", 5, 0, "日本語辞書の語と一致"));
        }
        else if (Words.HasPrefix(letters))
        {
            output.Add(new Contribution("Dictionary", letters.Length >= 4 ? 4 : 3, 0, "日本語辞書の語の先頭と一致"));
        }
    }
}
