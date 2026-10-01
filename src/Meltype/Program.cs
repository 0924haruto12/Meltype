// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Config;
using Meltype.Diagnostics;
using Meltype.UI;

namespace Meltype;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Meltype.exe --selftest [結果ファイル]: キーボードフックを掛けずに、主な機能が動くかだけを確かめる。
        if (args.FirstOrDefault() == "--selftest") return SelfTest.Run(args.ElementAtOrDefault(1));
        // Meltype.exe --exit: 動いている Meltype を終了させる (インストール・アンインストール用。管理者として動いていても止められる)。
        if (args.FirstOrDefault() == "--exit") return ExitSignal.Send() ? 0 : 1;

        // フックを二重に掛けると同じ打鍵を二重に保留・再入力してしまうので、多重起動させない。
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Meltype.SingleInstance", out var createdNew);
        if (!createdNew) return 0;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error($"UI で例外: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { Log.Error($"未処理の例外: {e.ExceptionObject}"); Log.FlushFile(); };

        AppPaths.MigrateFromOldName();
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var settings = Settings.Load(AppPaths.ConfigFile);
        if (!File.Exists(AppPaths.ConfigFile))
        {
            try { settings.Save(AppPaths.ConfigFile); } catch { }
        }

        MeltypeEngine engine;
        try
        {
            engine = new MeltypeEngine(settings, AppPaths.ConfigFile, AppPaths.ModelFile, AppPaths.UserDictionaryDirectory);
            engine.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Meltype を開始できませんでした。\n\n{ex.Message}", "Meltype", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using (engine)
        {
            using var exitSignal = new ExitSignal();
            Application.Run(new TrayApplicationContext(engine));
        }
        return 0;
    }
}
