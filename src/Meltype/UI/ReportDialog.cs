// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Diagnostics;

namespace Meltype.UI;

/// <summary>
/// 不具合の報告 (トレイの「不具合を報告...」)。フォームに入れる実行環境と、貼り付けてもらう最近のログを見せる。
/// ログは中身を確かめて (見られて困る部分は消して) からコピーしてもらう。実行環境はフォームを開くときに自動で入る。
/// </summary>
internal sealed class ReportDialog : Form
{
    private readonly TextBox _log;
    private readonly string _environment;

    public ReportDialog(Config.Settings settings)
    {
        Text = "Meltype 不具合の報告";
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 9.5F);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(720, area.Width - 40), Math.Min(620, area.Height - 60));
        MinimumSize = new Size(480, 420);
        _environment = Diagnostics.ReportInfo.Environment(settings);

        var intro = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 10, 10, 6),
            MaximumSize = new Size(Width - 40, 0),
            Text = "「フォームを開く」を押すと、報告のフォームがブラウザーで開きます。下の実行環境は自動でフォームに入ります。\n"
                + "ログは「ログをコピー」を押して、フォームの「ログ」の欄に貼り付けてください。見られて困る部分があれば、ここで消してからコピーしてください。",
        };

        var environment = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = _environment.Replace("\n", "\r\n") };
        var environmentBox = new GroupBox { Dock = DockStyle.Top, Height = 150, Text = "実行環境 (自動でフォームに入ります)", Padding = new Padding(8) };
        environmentBox.Controls.Add(environment);

        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            Text = Diagnostics.ReportInfo.RecentLog().Replace("\n", "\r\n"),
        };
        var logBox = new GroupBox { Dock = DockStyle.Fill, Text = "最近のログ (編集できます)", Padding = new Padding(8) };
        logBox.Controls.Add(_log);

        var copy = new Button { Text = "ログをコピー", AutoSize = true };
        var open = new Button { Text = "フォームを開く", AutoSize = true };
        var close = new Button { Text = "閉じる", AutoSize = true, DialogResult = DialogResult.Cancel };
        var copied = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 8, 0, 0) };
        copy.Click += (_, _) =>
        {
            try
            {
                if (_log.TextLength == 0) return;
                Clipboard.SetText(_log.Text);
                copied.Text = "コピーしました";
            }
            catch (Exception ex)
            {
                copied.Text = "コピーできませんでした";
                Diagnostics.Log.Warn($"ログをコピーできませんでした: {ex.Message}");
            }
        };
        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(AppInfo.ReportUrl(_environment)) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Warn($"報告の画面を開けませんでした: {ex.Message}");
            }
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        buttons.Controls.AddRange([close, open, copy, copied]);

        Controls.Add(logBox);
        Controls.Add(environmentBox);
        Controls.Add(intro);
        Controls.Add(buttons);
        CancelButton = close;
    }
}
