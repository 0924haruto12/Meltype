// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Reflection;

namespace AutoIME;

/// <summary>バージョン・著作権・ライセンスの表示 (トレイの「AutoIME について...」)。</summary>
internal static class AppInfo
{
    public const string SourceUrl = "https://github.com/yksr-melt/AutoIME";
    public const string CommercialContact = "ibutya0319@gmail.com";

    public static string Version =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static string AboutText => $"""
        AutoIME {Version}
        Copyright (C) 2026 雪代 / Yukishiro (@yksr_melt / @yksr-melt)

        このプログラムはフリーソフトウェアです。GNU General Public License
        (バージョン 3、またはそれ以降のバージョン) の条件で再配布・改変できます。
        このプログラムは無保証です。詳しくは GNU GPL を参照してください。
        https://www.gnu.org/licenses/gpl-3.0.html

        ソースコード: {SourceUrl}
        製品への組み込み (商用ライセンス): {CommercialContact}

        同梱の .NET ランタイムは MIT ライセンスです (app\dotnet\LICENSE.txt)。
        """;
}
