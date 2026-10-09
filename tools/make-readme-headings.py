# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro
# README の見出し画像 (docs/images/headings/*.svg) を作り直す。
#   pip install fonttools
#   curl -L -o mplus.ttf https://cdn.jsdelivr.net/gh/google/fonts@main/ofl/mplusrounded1c/MPLUSRounded1c-ExtraBold.ttf
#   python3 tools/make-readme-headings.py docs/images/headings
import math, sys
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

font = TTFont("mplus.ttf")
gs = font.getGlyphSet()
cmap = font.getBestCmap()
upm = font["head"].unitsPerEm
hmtx = font["hmtx"]

THEMES = {
    "blue": ("#3aaee8", "#7fd0f5"),
    "pink": ("#ff6f9f", "#ffa6c6"),
}
OUTLINE = "#7391ee"
DASH = "#ff8ab4"

def text_path(text, size, x0, baseline):
    s = size / upm
    pen = SVGPathPen(gs)
    x = x0
    for ch in text:
        g = cmap.get(ord(ch))
        if g is None:
            continue
        gs[g].draw(TransformPen(pen, (s, 0, 0, -s, x, baseline)))
        x += hmtx[g][0] * s
    return pen.getCommands(), x

def snowflake(cx, cy, r, color):
    parts = []
    for k in range(6):
        a = math.radians(90 + 60 * k)
        ex, ey = cx + r * math.cos(a), cy - r * math.sin(a)
        parts.append(f"M{cx:.1f} {cy:.1f}L{ex:.1f} {ey:.1f}")
        for t, b in ((0.55, 0.38),):
            bx, by = cx + r * t * math.cos(a), cy - r * t * math.sin(a)
            for d in (-1, 1):
                aa = a + d * math.radians(45)
                parts.append(f"M{bx:.1f} {by:.1f}L{bx + r * b * math.cos(aa):.1f} {by - r * b * math.sin(aa):.1f}")
    d = "".join(parts)
    return (f'<path d="{d}" stroke="{OUTLINE}" stroke-width="9" stroke-linecap="round" fill="none"/>'
            f'<path d="{d}" stroke="#ffffff" stroke-width="6" stroke-linecap="round" fill="none"/>'
            f'<path d="{d}" stroke="{color}" stroke-width="3" stroke-linecap="round" fill="none"/>')

def heading(text, theme, out, width=760, height=84, size=42):
    top, bottom = THEMES[theme]
    baseline = 58
    x0 = 66
    d, x_end = text_path(text, size, x0, baseline)
    dash_y = 62
    dash = (f'<path d="M{x_end + 18:.1f} {dash_y} H{width - 24}" stroke="{OUTLINE}" stroke-width="9" stroke-linecap="round"/>'
            f'<path d="M{x_end + 18:.1f} {dash_y} H{width - 24}" stroke="#ffffff" stroke-width="6" stroke-linecap="round"/>'
            f'<path d="M{x_end + 22:.1f} {dash_y} H{width - 28}" stroke="{DASH}" stroke-width="3" stroke-linecap="round" stroke-dasharray="10 9"/>')
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-label="{text}">
<title>{text}</title>
<defs><linearGradient id="g" x1="0" y1="{baseline - size * 0.9:.1f}" x2="0" y2="{baseline + 4}" gradientUnits="userSpaceOnUse">
<stop offset="0" stop-color="{top}"/><stop offset="1" stop-color="{bottom}"/></linearGradient></defs>
{snowflake(30, 42, 20, top)}
{dash}
<path d="{d}" fill="{OUTLINE}" stroke="{OUTLINE}" stroke-width="10" stroke-linejoin="round" transform="translate(0 2.5)"/>
<path d="{d}" fill="{OUTLINE}" stroke="{OUTLINE}" stroke-width="10" stroke-linejoin="round"/>
<path d="{d}" fill="#ffffff" stroke="#ffffff" stroke-width="6" stroke-linejoin="round"/>
<path d="{d}" fill="url(#g)"/>
</svg>
'''
    open(out, "w", encoding="utf-8").write(svg)

if __name__ == "__main__":
    outdir = sys.argv[1]
    items = [("features", "できること"), ("install", "インストール"), ("start", "使い始める"), ("faq", "よくある質問"),
             ("privacy", "プライバシー"), ("license", "ライセンス"), ("thanks", "協力してくださった方々"), ("develop", "開発に参加する"), ("stars", "Star History")]
    for i, (name, text) in enumerate(items):
        heading(text, "blue" if i % 2 == 0 else "pink", f"{outdir}/{name}.svg")
