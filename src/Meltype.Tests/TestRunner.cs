// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

namespace Meltype.Tests;

/// <summary>
/// Windows 版のテストと調査用の道具。Meltype.Core.Tests のテスト (OS に依存しない部分) もまとめて流し、
/// そのときは Windows のスペルチェッカーを使う。
/// </summary>
internal static class TestRunner
{
    [STAThread]
    public static int Main(string[] args)
    {
        TestSupport.WordChecker = Detection.WindowsSpellChecker.Shared;
        // dotnet run --project src/Meltype.Tests -- --convert きょうはいいてんきです
        // で、Microsoft IME の変換エンジン (MSIME.Japan) が使えるかを確かめる。
        if (args.FirstOrDefault() == "--type")
        {
            // dotnet run --project src/Meltype.Tests -- --type "ke-kiwotabeta "
            // 本物の変換エンジンをつないだ変換ボックスに 1 文字ずつ打ち、表示の変化を見る (ライブ変換 ON)。
            // 本物のアプリと同じく Windows のスペルチェッカーも使う。
            CompositionTests.Detector.SpellChecker = Detection.WindowsSpellChecker.Shared;
            using var ime = new Composition.MsImeKanjiConverter();
            using var winrt = new Composition.WinRtCandidates();
            // native\mozc\bin に Mozc の変換ヘルパーがあれば、アプリと同じく Mozc + Microsoft IME (両方) で変換する。
            // MELTYPE_ENGINE=System なら Microsoft IME だけ。
            using var mozc = new Composition.MozcConverter(Path.GetFullPath(Path.Combine("native", "mozc", "bin", "meltype_mozc_helper.exe")), Path.Combine(Path.GetTempPath(), "meltype-mozc-profile"));
            var engine = Enum.TryParse<Config.ConversionEngine>(Environment.GetEnvironmentVariable("MELTYPE_ENGINE"), out var chosen) ? chosen : Config.ConversionEngine.Hybrid;
            var converter = new Composition.HybridConverter(() => engine, mozc.IsInstalled ? mozc : null, ime, r => winrt.Get(r));
            Console.WriteLine($"変換エンジン: {(mozc.IsInstalled && engine != Config.ConversionEngine.System ? "Mozc + Microsoft IME" : "Microsoft IME")}");
            foreach (var text in args.Skip(1))
            {
                // "前の文字列|打つキー" の形なら、前の文字列をキャレットの前にある確定済みの文字として扱う。
                var bar = text.IndexOf('|');
                var keyboard = new CompositionTests.Keyboard(live: true, converter: converter, moreCandidates: converter.Candidates, userDictionary: new Composition.UserDictionary(null));
                if (bar >= 0) keyboard.Host.PrecedingText = text[..bar];
                foreach (var c in bar >= 0 ? text[(bar + 1)..] : text)
                {
                    keyboard.Type(c.ToString());
                    var view = keyboard.Host.View;
                    Console.WriteLine($"  {c} → {(view is null ? "(なし)" : view.Converting ? "[" + string.Join("|", view.Clauses!) + "] 候補: " + string.Join(",", view.Candidates) : view.Text)}");
                }
                Console.WriteLine($"  確定: {string.Join("|", keyboard.Host.Output)}");
                Console.WriteLine();
            }
            return 0;
        }
        if (args.FirstOrDefault() == "--winmd") { WinMdProbe.Run(args[1], args.Skip(2)); return 0; }
        if (args.FirstOrDefault() == "--candidates") { using var winrt = new Composition.WinRtCandidates(); foreach (var r in args.Skip(1)) Console.WriteLine($"{r}: {string.Join(", ", winrt.Get(r))}"); foreach (var e in Diagnostics.Log.Snapshot()) Console.WriteLine(e); return 0; }
        if (args.FirstOrDefault() == "--mozc")
        {
            // dotnet run --project src/Meltype.Tests -- --mozc <meltype_mozc_helper.exe> はははきょうしょくじにいきました ...
            // Mozc と Microsoft IME の変換結果を並べて比べる。
            using var mozc = new Composition.MozcConverter(args[1], Path.Combine(Path.GetTempPath(), "meltype-mozc-profile"));
            using var ime = new Composition.MsImeKanjiConverter();
            foreach (var reading in args.Skip(2))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var clauses = mozc.ConvertClauses(reading);
                var elapsed = watch.ElapsedMilliseconds;
                Console.WriteLine(reading);
                Console.WriteLine($"  Mozc ({elapsed}ms): {(clauses is null ? "(失敗)" : string.Join("|", clauses.Select(c => c.Text)))}");
                Console.WriteLine($"  IME : {string.Join("|", ime.ConvertClauses(reading)?.Select(c => c.Text) ?? [])}");
            }
            foreach (var entry in Diagnostics.Log.Snapshot()) Console.WriteLine($"  {entry}");
            return 0;
        }
        if (args.FirstOrDefault() == "--reading") { using var c = new Composition.MsImeKanjiConverter(); foreach (var t in args.Skip(1)) Console.WriteLine($"{t} → {c.Reading(t) ?? "(なし)"}"); return 0; }
        if (args.FirstOrDefault() == "--gen-emoji") { EmojiGenerator.Run(args[1], args[2], args[3]); return 0; }
        if (args.FirstOrDefault() == "--eval") { Quality.Print(Quality.Run()); return 0; }
        if (args.FirstOrDefault() == "--render-forms")
        {
            // 調査用: 設定画面とユーザー辞書の画面を表示せずに画像にする。
            Application.EnableVisualStyles();
            var engine = new MeltypeEngine(new Config.Settings(), null, null, null);
            using var invoker = new Control();
            invoker.CreateControl();
            using var service = new Composition.CompositionService(invoker, Composition.CompositionDetector.CreateDefault(), new Composition.CompositionOptions { UserDictionary = new Composition.UserDictionary(null) });
            service.UserDictionary.Add("きごうとう", "記号等");
            var composition = new Composition.CompositionWindow();
            composition.ShowView(new Composition.CompositionView("えがお", ["笑顔", "😊", "😄", "☺️", "(^^)", "(*^^*)", "(´▽｀)"], 1, true, "Space/↓ 候補"), new Point(-5000, -5000));
            var indicator = new Composition.ModeIndicatorWindow();
            indicator.Flash(true, new Point(-5000, -5000));
            foreach (var form in new Form[] { new UI.SettingsForm(engine), new UI.UserDictionaryForm(service), indicator, composition })
            {
                using (form)
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-5000, -5000);
                    form.Show();
                    Application.DoEvents();
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(Path.Combine(args[1], form.GetType().Name + ".png"));
                }
            }
            return 0;
        }
        if (args.FirstOrDefault() == "--runtime-closure") { RuntimeClosure.Run(args[1]); return 0; }
        if (args.FirstOrDefault() == "--context")
        {
            // dotnet run --project src/Meltype.Tests -- --context 財布の:かわ 川に:はし
            using var converter = new Composition.MsImeKanjiConverter();
            foreach (var pair in args.Skip(1))
            {
                var parts = pair.Split(':');
                Console.WriteLine($"{parts[0]} + {parts[1]}: 単独={converter.Convert(parts[1])}  " +
                    string.Join("  ", new uint[] { 0x10, 0x20, 0x30 }.Select(f => $"0x{f:X}={converter.ProbeWithContext(parts[0], parts[1], f)}")) +
                    $"  読みごと={converter.Convert(parts[0] + parts[1])}");
            }
            return 0;
        }
        if (args.FirstOrDefault() == "--convert")
        {
            using var converter = new Composition.MsImeKanjiConverter();
            foreach (var text in args.Skip(1))
            {
                Console.WriteLine($"{text} → {converter.Convert(text) ?? "(変換できない)"}");
                var clauses = converter.ConvertClauses(text);
                Console.WriteLine("  解析: " + converter.DescribeMorph(text));
                Console.WriteLine("  文脈付き (この本は): " + string.Join(" | ", converter.ConvertClauses(text, "この本は")?.Select(c => $"{c.Reading}={c.Text}") ?? ["(取れない)"]));
                Console.WriteLine("  文節: " + (clauses is null ? "(取れない)" : string.Join(" | ", clauses.Select(c => $"{c.Reading}={c.Text}"))));
            }
            foreach (var entry in Diagnostics.Log.Snapshot()) Console.WriteLine(entry);
            return converter.IsAvailable ? 0 : 1;
        }

        if (args.FirstOrDefault() == "--explain") { TestHost.Explain(args.Skip(1)); return 0; }

        return TestHost.Run([typeof(TestSupport).Assembly, typeof(TestRunner).Assembly], args.FirstOrDefault());
    }
}
