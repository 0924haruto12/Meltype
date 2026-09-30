// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Text;

namespace Meltype.Input;

/// <summary>コードの行の、キャレットの位置がどこか。</summary>
public enum LineKind
{
    /// <summary>コード (英数が基本)。</summary>
    Code,
    /// <summary>コメントの中 (// # -- /* <!-- など)。日本語を書くことが多い。</summary>
    Comment,
    /// <summary>文字列の中 ("…" '…' `…`)。日本語を書くことが多い。</summary>
    String,
}

/// <summary>
/// コードエディター・ターミナル (アプリの種類が「コード」) で、今の行のキャレットより前の文字列から、
/// キャレットがコメントや文字列の中にあるか (日本語を書く場所か) を調べる。
/// 複数行のコメント・文字列 (/* … */ の途中の行、""" … """) は、行頭の * 以外は分からない。
/// </summary>
public static class LineContext
{
    public static LineKind Classify(string line)
    {
        char? quote = null;
        var firstText = true; // まだ空白以外の文字が出ていない
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is { } q)
            {
                if (c == '\\') i++; // エスケープ (\" は文字列の終わりではない)
                else if (c == q) quote = null;
                continue;
            }
            if (char.IsWhiteSpace(c)) continue;
            var atStart = firstText;
            firstText = false;

            if (c is '"' or '`' || (c == '\'' && !(i > 0 && char.IsLetterOrDigit(line[i - 1]))))
            {
                // ' は英単語の中 (don't) なら文字列の始まりではない。
                quote = c;
                continue;
            }
            if (StartsWith(line, i, "//")) return LineKind.Comment;
            if (StartsWith(line, i, "/*"))
            {
                var close = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0) return LineKind.Comment;
                i = close + 1;
                continue;
            }
            if (StartsWith(line, i, "<!--"))
            {
                var close = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
                if (close < 0) return LineKind.Comment;
                i = close + 2;
                continue;
            }
            // # コメント (Python・シェル・PowerShell・YAML)。#include や #region のように # の直後が文字なら指令なので除く。
            if (c == '#' && (i + 1 == line.Length || char.IsWhiteSpace(line[i + 1]) || line[i + 1] == '#')) return LineKind.Comment;
            // -- コメント (SQL・Lua・Haskell)。x-- のような減算は直後が空白でないことが多い。
            if (StartsWith(line, i, "--") && (i + 2 == line.Length || line[i + 2] == ' ') && (atStart || i > 0 && line[i - 1] == ' ')) return LineKind.Comment;
            // 複数行コメントの途中の行 ( * …)
            if (atStart && c == '*' && (i + 1 == line.Length || line[i + 1] == ' ')) return LineKind.Comment;
            // REM コメント (バッチファイル)
            if (atStart && StartsWithIgnoreCase(line, i, "rem ")) return LineKind.Comment;
        }
        return quote is null ? LineKind.Code : LineKind.String;
    }

    private static bool StartsWith(string text, int index, string value) =>
        string.CompareOrdinal(text, index, value, 0, value.Length) == 0 && index + value.Length <= text.Length;

    private static bool StartsWithIgnoreCase(string text, int index, string value) =>
        index + value.Length <= text.Length && string.Compare(text, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>ウィンドウのタイトルが文章のファイル (README.md など) を開いているか。コードエディターでも文章なら「一般」として扱う。</summary>
    public static bool IsDocumentTitle(string title)
    {
        foreach (var extension in (ReadOnlySpan<string>)[".md", ".markdown", ".txt", ".rst", ".adoc", ".org", ".tex"])
        {
            var index = title.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && (index + extension.Length == title.Length || !char.IsLetterOrDigit(title[index + extension.Length]))) return true;
        }
        return false;
    }
}

/// <summary>
/// 今の行のキャレットより前の文字列を、打鍵から追いかける (フックのスレッドと UI スレッドから呼ばれる)。
/// キャレットが動いた (矢印キー・クリック・フォーカスの変化・Ctrl の操作) ら分からなくなり、UI Automation で読み直す。
/// </summary>
public sealed class LineTracker
{
    private readonly object _gate = new();
    private readonly StringBuilder _line = new();
    private bool _known;

    /// <summary>今の行のキャレットより前の文字列。分からなければ null。</summary>
    public string? Text
    {
        get { lock (_gate) return _known ? _line.ToString() : null; }
    }

    public void Append(string text)
    {
        lock (_gate)
        {
            if (!_known) return;
            foreach (var c in text)
            {
                if (c is '\r' or '\n') _line.Clear();
                else _line.Append(c);
            }
            if (_line.Length > 500) _line.Remove(0, _line.Length - 500);
        }
    }

    public void Backspace()
    {
        lock (_gate)
        {
            if (!_known) return;
            if (_line.Length > 0) _line.Length--;
            else _known = false; // 前の行とつながった
        }
    }

    /// <summary>Enter: 新しい行 (自動のインデントは分からないが、空白なので判定には影響しない)。</summary>
    public void NewLine()
    {
        lock (_gate)
        {
            _line.Clear();
            _known = true;
        }
    }

    /// <summary>キャレットが動いたかもしれない。</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _line.Clear();
            _known = false;
        }
    }

    public bool IsKnown
    {
        get { lock (_gate) return _known; }
    }

    /// <summary>UI Automation で読んだキャレットより前の文字列 (最後の改行より後ろを使う)。</summary>
    public void SetFromText(string before)
    {
        var newline = before.LastIndexOfAny(['\r', '\n']);
        lock (_gate)
        {
            _line.Clear();
            _line.Append(newline >= 0 ? before[(newline + 1)..] : before);
            _known = true;
        }
    }
}
