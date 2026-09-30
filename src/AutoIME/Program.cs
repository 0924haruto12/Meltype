// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using AutoIME.Config;
using AutoIME.Diagnostics;
using AutoIME.UI;

namespace AutoIME;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // AutoIME.exe --selftest [結果ファイル]: キーボードフックを掛けずに、主な機能が動くかだけを確かめる。
        if (args.FirstOrDefault() == "--selftest") return SelfTest.Run(args.ElementAtOrDefault(1));

        // フックを二重に掛けると同じ打鍵を二重に保留・再入力してしまうので、多重起動させない。
        using var mutex = new Mutex(initiallyOwned: true, @"Local\AutoIME.SingleInstance", out var createdNew);
        if (!createdNew) return 0;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error($"UI で例外: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { Log.Error($"未処理の例外: {e.ExceptionObject}"); Log.FlushFile(); };

        Directory.CreateDirectory(AppPaths.DataDirectory);
        var settings = Settings.Load(AppPaths.ConfigFile);
        if (!File.Exists(AppPaths.ConfigFile))
        {
            try { settings.Save(AppPaths.ConfigFile); } catch { }
        }

        AutoImeEngine engine;
        try
        {
            engine = new AutoImeEngine(settings, AppPaths.ConfigFile, AppPaths.ModelFile, AppPaths.UserDictionaryDirectory);
            engine.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"AutoIME を開始できませんでした。\n\n{ex.Message}", "AutoIME", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using (engine)
        {
            Application.Run(new TrayApplicationContext(engine));
        }
        return 0;
    }
}
