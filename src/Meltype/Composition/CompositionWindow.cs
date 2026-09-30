// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Drawing.Drawing2D;

namespace Meltype.Composition;

/// <summary>
/// 変換ボックス。フォーカスを奪わない最前面のウィンドウで、カーソル (キャレット) の下に出す。
/// 未確定の文字列に下線を引き、変換中は候補の一覧を出す。
/// </summary>
internal sealed class CompositionWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_TOPMOST = 0x00000008;
    private static readonly Color Background = Color.FromArgb(32, 34, 40);
    private static readonly Color Accent = Color.FromArgb(76, 160, 255);
    private readonly Font _textFont = new("Yu Gothic UI", 13F);
    private readonly Font _candidateFont = new("Yu Gothic UI", 11F);
    private readonly Font _hintFont = new("Yu Gothic UI", 8.5F);
    private CompositionView? _view;

    public CompositionWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Background;
        DoubleBuffered = true;
        Size = new Size(200, 40);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    /// <summary>表示内容を更新する。anchor は表示位置 (キャレットの左下)。null なら今の位置のまま。</summary>
    public void ShowView(CompositionView view, Point? anchor)
    {
        _view = view;
        var size = Measure(view);
        var location = anchor ?? Location;
        // 画面からはみ出さないようにする。
        var screen = Screen.FromPoint(location).WorkingArea;
        if (location.X + size.Width > screen.Right) location.X = Math.Max(screen.Left, screen.Right - size.Width);
        if (location.Y + size.Height > screen.Bottom) location.Y = Math.Max(screen.Top, location.Y - size.Height - 28);
        SetBounds(location.X, location.Y, size.Width, size.Height);
        if (!Visible) Show();
        Invalidate();
    }

    private Size Measure(CompositionView view)
    {
        using var g = CreateGraphics();
        var width = TextRenderer.MeasureText(g, view.Text, _textFont).Width + 20;
        if (view.Clauses is { Count: > 0 } clauses)
        {
            var clausesWidth = clauses.Sum(c => TextRenderer.MeasureText(g, c, _textFont, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width + 4);
            width = Math.Max(width, clausesWidth + 20);
        }
        var height = _textFont.Height + 16;
        if (view.Converting)
        {
            foreach (var candidate in view.Candidates)
            {
                width = Math.Max(width, TextRenderer.MeasureText(g, $"9  {candidate}", _candidateFont).Width + 28);
            }
            height += view.Candidates.Count * (_candidateFont.Height + 4) + 6;
        }
        width = Math.Max(width, TextRenderer.MeasureText(g, view.Hint, _hintFont).Width + 16);
        height += _hintFont.Height + 6;
        return new Size(Math.Min(width, 900), height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var view = _view;
        if (view is null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var border = new Pen(Accent)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        var y = 8;
        if (view.Clauses is { Count: > 0 } clauses)
        {
            // 変換中: 文節ごとに下線を引き、選択中の文節は背景を付けて太線にする。
            const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var x = 10;
            for (var i = 0; i < clauses.Count; i++)
            {
                var width = TextRenderer.MeasureText(g, clauses[i], _textFont, Size.Empty, flags).Width;
                var selected = i == view.SelectedClause;
                if (selected)
                {
                    using var highlight = new SolidBrush(Color.FromArgb(90, 76, 160, 255));
                    g.FillRectangle(highlight, x - 1, y - 1, width + 2, _textFont.Height + 2);
                }
                TextRenderer.DrawText(g, clauses[i], _textFont, new Point(x, y), Color.White, flags);
                using (var underline = new Pen(selected ? Accent : Color.FromArgb(170, 170, 170), selected ? 3 : 1))
                {
                    g.DrawLine(underline, x + 1, y + _textFont.Height + 1, x + width - 2, y + _textFont.Height + 1);
                }
                x += width + 4;
            }
            y += _textFont.Height + 8;
        }
        else
        {
            TextRenderer.DrawText(g, view.Text, _textFont, new Point(10, y), Color.White, TextFormatFlags.NoPrefix);
            var textWidth = TextRenderer.MeasureText(g, view.Text, _textFont).Width;
            y += _textFont.Height;
            using (var underline = new Pen(Color.White, 1) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(underline, 12, y, 10 + textWidth - 4, y);
            }
            y += 8;
        }

        if (view.Converting)
        {
            for (var i = 0; i < view.Candidates.Count; i++)
            {
                var rowHeight = _candidateFont.Height + 4;
                if (i == view.SelectedIndex)
                {
                    using var highlight = new SolidBrush(Color.FromArgb(60, 76, 160, 255));
                    g.FillRectangle(highlight, 4, y - 2, Width - 8, rowHeight);
                }
                TextRenderer.DrawText(g, $"{i + 1}  {view.Candidates[i]}", _candidateFont, new Point(12, y),
                    i == view.SelectedIndex ? Color.White : Color.FromArgb(200, 200, 200), TextFormatFlags.NoPrefix);
                y += rowHeight;
            }
            y += 6;
        }
        TextRenderer.DrawText(g, view.Hint, _hintFont, new Point(8, y), Color.FromArgb(150, 150, 150), TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textFont.Dispose();
            _candidateFont.Dispose();
            _hintFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
