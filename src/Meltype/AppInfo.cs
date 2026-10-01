// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Reflection;

namespace Meltype;

/// <summary>バージョン・著作権・ライセンスの表示 (トレイの「Meltype について...」)。</summary>
internal static class AppInfo
{
    public const string SourceUrl = "https://github.com/yksr-melt/Meltype";

    /// <summary>自動更新で最新のリリースを見に行く GitHub のリポジトリ (公開されている必要がある)。</summary>
    public const string UpdateRepository = "yksr-melt/Meltype";
    public const string CommercialContact = "ibutya0319@gmail.com";

    public static string Version =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static string AboutText => $"""
        Meltype {Version}
        Copyright (C) 2026 雪代 / Yukishiro (@yksr_melt / @yksr-melt)

        このプログラムはフリーソフトウェアです。GNU General Public License
        (バージョン 3、またはそれ以降のバージョン) の条件で再配布・改変できます。
        このプログラムは無保証です。詳しくは GNU GPL を参照してください。
        https://www.gnu.org/licenses/gpl-3.0.html

        ソースコード: {SourceUrl}
        製品への組み込み (商用ライセンス): {CommercialContact}

        同梱の .NET ランタイムは MIT ライセンスです (app\dotnet\LICENSE.txt)。
        変換エンジン Mozc は BSD-3-Clause ライセンスです (app\mozc\MOZC-LICENSE.txt、辞書は app\mozc\MOZC-CREDITS.html)。
        英訳の候補・英字で書く語の候補・打ち間違いを直すための読みの一覧は JMdict (Electronic Dictionary Research and Development Group) を元にしています (CC BY-SA 4.0)。
        絵文字の読みは Unicode CLDR を元にしています (Unicode License v3)。
        詳しくは THIRD-PARTY-NOTICES.txt を参照してください。
        """;
}
