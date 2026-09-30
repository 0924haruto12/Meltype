// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Collections.Concurrent;

namespace AutoIME.Composition;

/// <summary>フォーカスのある要素の情報。</summary>
public sealed record FocusInfo(bool IsTextInput, bool IsPassword, Rectangle? Bounds, string Description);

/// <summary>
/// フォーカスのある要素を UI Automation で調べる。
///   ・文字を入力する欄かどうか: 変換ボックスは、文字入力欄以外 (Gmail の j/k、エクスプローラーの頭文字検索、ゲーム) で
///     キーを横取りしてはいけないし、パスワード欄の文字を画面に表示してもいけない。
///   ・キャレットの直前の確定済みの文字: 英語とも日本語とも読める語 (sushi) を前の文字に合わせるため。
/// UIA は他プロセスへの問い合わせで時間がかかることがあるため専用スレッドで行い、フックからは
/// 結果のキャッシュだけを見る。フォーカスが変わってから調べ終わるまでの間は「入力欄ではない」扱い
/// (= 横取りしない) にするので、調べ損ねても安全側に倒れる。
/// </summary>
public sealed class FocusInspector : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private long _focusSequence;
    private long _resolvedSequence = -1;
    private volatile FocusInfo _info = new(false, false, null, "未確認");
    private string? _loggedDescription;
    private (string Description, Rectangle? Bounds)? _lastTextInput;

    /// <summary>
    /// 別の入力欄 (パスワード以外) にフォーカスが移った (このクラスのスレッドから呼ばれる)。入力モード (あ / A) の表示に使う。
    /// </summary>
    public event Action? TextInputEntered;

    public FocusInspector()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "AutoIME focus inspector" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    /// <summary>フォーカスが変わったとき (フックのスレッドから呼ばれる。ブロックしない)。</summary>
    public void Invalidate()
    {
        var sequence = Interlocked.Increment(ref _focusSequence);
        Enqueue(() => InspectIfLatest(sequence));
    }

    /// <summary>最新のフォーカスについて調べ終わっていて、パスワード以外の入力欄なら true。</summary>
    public bool CanCapture
    {
        get
        {
            if (Interlocked.Read(ref _resolvedSequence) != Interlocked.Read(ref _focusSequence)) return false;
            var info = _info;
            return info.IsTextInput && !info.IsPassword;
        }
    }

    public FocusInfo Current => _info;

    /// <summary>キャレットの前後の文字列 (それぞれ最大 20 文字) を調べて callback(前, 後ろ) に渡す (このクラスのスレッドから呼ばれる)。</summary>
    public void RequestSurroundingText(Action<string?, string?> callback) => Enqueue(() =>
    {
        var (before, after) = ReadSurroundingText();
        callback(before, after);
    });

    private void Enqueue(Action action)
    {
        if (!_work.IsAddingCompleted)
        {
            try { _work.Add(action); }
            catch (InvalidOperationException) { }
        }
    }

    private void Run()
    {
        foreach (var action in _work.GetConsumingEnumerable())
        {
            try { action(); }
            catch (Exception ex) { Diagnostics.Log.Warn($"フォーカスの確認で例外: {ex.Message}"); }
        }
    }

    private void InspectIfLatest(long sequence)
    {
        // 連続したフォーカス変更は、最新の 1 回だけ調べる。
        if (sequence != Interlocked.Read(ref _focusSequence)) return;
        var info = Inspect();
        _info = info;
        // 調べている間にまたフォーカスが変わっていたら、この結果は採用しない (次の要求で調べ直す)。
        if (sequence == Interlocked.Read(ref _focusSequence)) Interlocked.Exchange(ref _resolvedSequence, sequence);

        if (info.IsTextInput && !info.IsPassword)
        {
            var key = (info.Description, info.Bounds);
            if (_lastTextInput != key)
            {
                _lastTextInput = key;
                TextInputEntered?.Invoke();
            }
        }
        else _lastTextInput = null;

        // 変換ボックスが出ない理由を後から追えるように、判断が変わったらログに残す。
        var summary = $"{(info.IsPassword ? "パスワード欄" : info.IsTextInput ? "入力欄" : "入力欄ではない")}: {info.Description}";
        if (summary != _loggedDescription)
        {
            _loggedDescription = summary;
            Diagnostics.Log.Info($"フォーカス → {summary}{(info.IsTextInput && !info.IsPassword ? "" : " (変換ボックスは出しません)")}");
        }
    }

    private UiAutomation? _automation;
    private bool _automationUnavailable;

    /// <summary>UI Automation はこのクラスのスレッド (MTA) で作り、そのスレッドからだけ使う。</summary>
    private UiAutomation? Automation()
    {
        if (_automation is not null || _automationUnavailable) return _automation;
        try
        {
            _automation = new UiAutomation();
        }
        catch (Exception ex)
        {
            _automationUnavailable = true;
            Diagnostics.Log.Warn($"UI Automation を使えません。変換ボックスは出せません: {ex.Message}");
        }
        return _automation;
    }

    private FocusInfo Inspect()
    {
        try
        {
            var element = Automation()?.Focused();
            if (element is null) return new FocusInfo(false, false, null, "フォーカスなし");
            var type = element.ControlType;
            var description = $"{ControlTypeName(type)} \"{Trim(element.Name)}\" ({element.ClassName})";
            if (element.IsPassword) return new FocusInfo(true, true, element.Bounds, description);

            var editable = false;
            if (type is UiAutomation.ControlTypeEdit or UiAutomation.ControlTypeDocument or UiAutomation.ControlTypeComboBox)
            {
                editable = !(element.HasValuePattern && element.IsReadOnly);
            }
            else if (element.HasTextPattern && element.IsKeyboardFocusable)
            {
                // Windows Terminal などは Edit ではなく TextPattern を持つ独自コントロール。
                editable = true;
            }
            return new FocusInfo(editable, false, element.Bounds, description);
        }
        catch (Exception ex)
        {
            return new FocusInfo(false, false, null, $"確認できない: {ex.GetType().Name}");
        }
    }

    private static string ControlTypeName(int type) => type switch
    {
        UiAutomation.ControlTypeEdit => "Edit",
        UiAutomation.ControlTypeDocument => "Document",
        UiAutomation.ControlTypeComboBox => "ComboBox",
        _ => $"ControlType {type}",
    };

    /// <summary>
    /// キャレットの前後の文字列。TextPattern (Chrome, Word, メモ帳など) ならキャレット位置から前後 20 文字ずつ、
    /// 無ければ ValuePattern の値の末尾 (キャレットが末尾にあるとみなす) を前として返す。取れなければ null。
    /// </summary>
    private (string? Before, string? After) ReadSurroundingText()
    {
        try
        {
            var element = Automation()?.Focused();
            if (element is null || element.IsPassword) return (null, null);
            if (element.Surrounding(20) is { } surrounding) return surrounding;
            if (element.HasValuePattern)
            {
                var value = element.Value;
                return (value.Length > 20 ? value[^20..] : value, null);
            }
        }
        catch
        {
            // 取れなければ自分の確定履歴で判断する。
        }
        return (null, null);
    }

    /// <summary>自己診断用: UI Automation でデスクトップの要素を取れるか。</summary>
    public static string Probe() => new UiAutomation().Root() is { } root ? ControlTypeName(root.ControlType) : "(取れない)";

    private static string Trim(string text) => text.Length > 30 ? text[..30] + "…" : text;

    public void Dispose()
    {
        _work.CompleteAdding();
        _thread.Join(1000);
    }
}
