// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Diagnostics;
using System.Text;

namespace Meltype.Composition;

/// <summary>
/// Mozc (Google 日本語入力のオープンソース版、BSD-3-Clause) の変換エンジン。
/// 同梱の変換ヘルパー (meltype_mozc_helper) を別のプロセスで動かし、標準入出力で変換を頼む。
///
/// やり取り (UTF-8、1 行ずつ): 「C[TAB]前の文字列[TAB]読み」を送ると、文節ごとに「読み[US]候補1[US]候補2…」を [RS] でつないだ 1 行が返る。
/// ヘルパーが無い・起動できない・応答しないときは null を返すので、呼び出し側で別の変換エンジンを使う。
/// </summary>
public sealed class MozcConverter : IKanjiConverter, IDisposable
{
    private const char UnitSeparator = '\x1f';
    private const char RecordSeparator = '\x1e';
    private const int TimeoutMs = 1500;
    private const int StartTimeoutMs = 10000;

    private readonly string _helperPath;
    private readonly string? _profileDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _candidates = new(StringComparer.Ordinal);
    private Process? _process;
    private int _failures;

    /// <param name="helperPath">meltype_mozc_helper の実行ファイル。</param>
    /// <param name="profileDirectory">Mozc の学習データの保存先 (null なら Mozc の既定)。</param>
    public MozcConverter(string helperPath, string? profileDirectory)
    {
        _helperPath = helperPath;
        _profileDirectory = profileDirectory;
    }

    /// <summary>ヘルパーがあるか (起動できるかは使ってみるまで分からない)。</summary>
    public bool IsInstalled => File.Exists(_helperPath);

    /// <summary>使えるか。何度も失敗したら諦める (毎回起動し直して入力が遅くなるのを避ける)。</summary>
    public bool IsAvailable => IsInstalled && _failures < 3;

    public string? Convert(string hiragana) =>
        ConvertClauses(hiragana) is { Count: > 0 } clauses ? string.Concat(clauses.Select(c => c.Text)) : null;

    public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null)
    {
        if (Request(context ?? "", hiragana) is not { Count: > 0 } segments) return null;
        return segments.Select(s => new ConversionClause(s.Reading, s.Candidates.FirstOrDefault() ?? s.Reading)).ToList();
    }

    /// <summary>読みの変換候補 (多い順)。直前の変換で同じ読みの文節があればその候補、無ければその読みだけで変換する。</summary>
    public IReadOnlyList<string> Candidates(string reading)
    {
        lock (_gate)
        {
            if (_candidates.TryGetValue(reading, out var cached)) return cached;
        }
        var segments = Request("", reading);
        if (segments is null) return [];
        // 1 文節ならその候補。複数の文節に分かれたなら、最初の候補をつなげたものだけ。
        return segments.Count == 1 ? segments[0].Candidates : [string.Concat(segments.Select(s => s.Candidates.FirstOrDefault() ?? s.Reading))];
    }

    private List<(string Reading, IReadOnlyList<string> Candidates)>? Request(string context, string reading)
    {
        if (string.IsNullOrEmpty(reading) || !IsAvailable) return null;
        context = Clean(context);
        reading = Clean(reading);
        lock (_gate)
        {
            try
            {
                var process = Start();
                process.StandardInput.Write($"C\t{context}\t{reading}\n");
                process.StandardInput.Flush();
                var line = ReadLine(process, TimeoutMs);
                if (line is null)
                {
                    Diagnostics.Log.Warn($"Mozc が応答しませんでした ({TimeoutMs}ms)。");
                    Fail();
                    return null;
                }
                _failures = 0;
                if (line.Length == 0) return null;
                var segments = new List<(string, IReadOnlyList<string>)>();
                foreach (var record in line.Split(RecordSeparator))
                {
                    var fields = record.Split(UnitSeparator);
                    if (fields[0].Length == 0) continue;
                    var candidates = fields.Skip(1).Where(c => c.Length > 0).Distinct().ToList();
                    segments.Add((fields[0], candidates));
                    if (_candidates.Count > 512) _candidates.Clear();
                    _candidates[fields[0]] = candidates;
                }
                return segments;
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Warn($"Mozc を使えませんでした: {ex.Message}");
                Fail();
                return null;
            }
        }
    }

    // タブ・改行は区切りに使うので空白にする。
    private static string Clean(string text) => text.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    private Process Start()
    {
        if (_process is { HasExited: false } running) return running;
        _process?.Dispose();
        var info = new ProcessStartInfo(_helperPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            WorkingDirectory = Path.GetDirectoryName(_helperPath) ?? "",
        };
        if (_profileDirectory is not null)
        {
            Directory.CreateDirectory(_profileDirectory);
            info.ArgumentList.Add(_profileDirectory);
        }
        var process = Process.Start(info) ?? throw new InvalidOperationException("起動できませんでした");
        // Mozc のログ (標準エラー) は読み捨てる (溜まるとヘルパーが止まる)。
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        var ready = ReadLine(process, StartTimeoutMs);
        if (ready != "READY")
        {
            try { process.Kill(); } catch { }
            process.Dispose();
            throw new InvalidOperationException($"起動に失敗しました: {ready ?? "応答なし"}");
        }
        Diagnostics.Log.Info("Mozc の変換エンジンを起動しました。");
        _process = process;
        return process;
    }

    private static string? ReadLine(Process process, int timeoutMs)
    {
        var task = process.StandardOutput.ReadLineAsync();
        return task.Wait(timeoutMs) ? task.Result : null;
    }

    private void Fail()
    {
        _failures++;
        try
        {
            if (_process is { HasExited: false }) _process.Kill();
        }
        catch
        {
        }
        _process?.Dispose();
        _process = null;
    }

    /// <summary>起動しておく (最初の変換で待たないように)。</summary>
    public void WarmUp() => ThreadPool.QueueUserWorkItem(_ => Request("", "あ"));

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                if (_process is { HasExited: false } process)
                {
                    process.StandardInput.Write("Q\n");
                    process.StandardInput.Flush();
                    if (!process.WaitForExit(1000)) process.Kill();
                }
            }
            catch
            {
            }
            _process?.Dispose();
            _process = null;
        }
    }
}
