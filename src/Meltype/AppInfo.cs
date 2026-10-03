// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using System.Reflection;

namespace Meltype;

/// <summary>バージョン・著作権・ライセンスの表示 (トレイの「Meltype について...」)。</summary>
internal static class AppInfo
{
    public const string SourceUrl = "https://github.com/yksr-melt/Meltype";

    /// <summary>
    /// 不具合報告のフォーム (Google フォームの「事前入力した URL」。OS に Windows 11、版に 0.0.0、実行環境に ENV を入れて作ったもの)。
    /// 空なら GitHub の Issue の画面を開く。作り方は tools/report-form/README.md。
    /// </summary>
    public const string ReportForm = "";

    /// <summary>不具合報告を開く URL (OS・版・実行環境は今のものを入れる)。</summary>
    public static string ReportUrl(string environment)
    {
        if (ReportForm.Length == 0) return $"{SourceUrl}/issues/new/choose";
        return ReportForm
            .Replace("=Windows+11", "=" + Uri.EscapeDataString(Diagnostics.ReportInfo.OsName))
            .Replace("=0.0.0", "=" + Uri.EscapeDataString(Version))
            .Replace("=ENV", "=" + Uri.EscapeDataString(environment));
    }

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
        候補の意味はウィクショナリー日本語版 (Wiktionary の執筆者) を元にしています (CC BY-SA 4.0)。
        絵文字の読みは Unicode CLDR を元にしています (Unicode License v3)。
        詳しくは THIRD-PARTY-NOTICES.txt を参照してください。
        """;
}
