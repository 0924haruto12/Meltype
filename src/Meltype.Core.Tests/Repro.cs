// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Text.Json;

namespace Meltype.Tests;

/// <summary>
/// 報告された「打ったもの」を Meltype キーボードで打ってみる (GitHub の bot が誤判定の報告を再現するのに使う)。
/// 変換エンジンはテスト用の偽物なので、漢字の変換は再現しない。日本語 / 英語の判定 (にほんgo) を見る。
/// </summary>
internal static class Repro
{
    /// <summary>打つ文字の上限 (bot に長い文を渡されても時間がかからないように)。</summary>
    private const int MaxKeys = 300;

    public static void Type(string keys, string last)
    {
        // 英字・数字・記号と空白だけ (それ以外の文字はキーとして打てない)
        // bot が結果を読むので、ほかの環境の文字コードにせず UTF-8 で出す
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var typed = new string(keys.Where(c => c is >= ' ' and <= '~').Take(MaxKeys).ToArray()).Trim();
        var k = new CompositionTests.Keyboard();
        k.Type(typed);
        var showing = k.Showing;
        switch (last)
        {
            case "space": k.Type(" "); break;
            case "enter": k.Type("\n"); break;
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            typed,
            last,
            // 最後のキーの前に変換ボックスに出ていたもの
            showing,
            // 最後のキーを押した後: 入力欄に入ったもの + まだ変換ボックスにあるもの
            committed = k.Host.Document,
            // 英語のスペルチェッカーを使ったか (Windows では使う。使わないと英単語の判定が実際のアプリと違うことがある)
            spellChecker = CompositionTests.Detector.SpellChecker is not null,
            composing = k.Showing,
        }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
