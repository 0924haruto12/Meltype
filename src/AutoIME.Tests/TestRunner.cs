using System.Reflection;

namespace AutoIME.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class TestAttribute : Attribute;

internal sealed class AssertionException(string message) : Exception(message);

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }

    public static void Equal<T>(T expected, T actual, string message = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{message} 期待値: {expected} / 実際: {actual}".Trim());
    }
}

/// <summary>[Test] の付いた static メソッドをすべて実行する最小限のテストランナー。</summary>
internal static class TestRunner
{
    [STAThread]
    public static int Main(string[] args)
    {
        // dotnet run --project src/AutoIME.Tests -- --convert きょうはいいてんきです
        // で、Microsoft IME の変換エンジン (MSIME.Japan) が使えるかを確かめる。
        if (args.FirstOrDefault() == "--type")
        {
            // dotnet run --project src/AutoIME.Tests -- --type "ke-kiwotabeta "
            // 本物の変換エンジンをつないだ変換ボックスに 1 文字ずつ打ち、表示の変化を見る (ライブ変換 ON)。
            using var converter = new Composition.MsImeKanjiConverter();
            using var winrt = new Composition.WinRtCandidates();
            foreach (var text in args.Skip(1))
            {
                // "前の文字列|打つキー" の形なら、前の文字列をキャレットの前にある確定済みの文字として扱う。
                var bar = text.IndexOf('|');
                var keyboard = new CompositionTests.Keyboard(live: true, converter: converter, moreCandidates: r => winrt.Get(r));
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
        if (args.FirstOrDefault() == "--render-forms")
        {
            // 調査用: 設定画面とユーザー辞書の画面を表示せずに画像にする。
            Application.EnableVisualStyles();
            var engine = new AutoImeEngine(new Config.Settings(), null, null, null);
            using var invoker = new Control();
            invoker.CreateControl();
            using var service = new Composition.CompositionService(invoker, Composition.CompositionDetector.CreateDefault(), new Composition.CompositionOptions { UserDictionary = new Composition.UserDictionary(null) });
            service.UserDictionary.Add("きごうとう", "記号等");
            var indicator = new Composition.ModeIndicatorWindow();
            indicator.Flash(true, new Point(-5000, -5000));
            foreach (var form in new Form[] { new UI.SettingsForm(engine), new UI.UserDictionaryForm(service), indicator })
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
            // dotnet run --project src/AutoIME.Tests -- --context 財布の:かわ 川に:はし
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

        // dotnet run --project src/AutoIME.Tests -- --explain konnichiwa hello
        // で、1 文字ずつの判定とその理由を表示する (辞書・閾値の調整用)。
        if (args.FirstOrDefault() == "--explain")
        {
            var engine = TestSupport.CreateEngine();
            foreach (var word in args.Skip(1))
            {
                for (var i = 1; i <= word.Length; i++)
                {
                    var prefix = word[..i];
                    var result = engine.Evaluate(new Detection.DetectionInput(prefix, prefix.Select(c => (int)char.ToUpperInvariant(c)).ToArray(), i == word.Length));
                    Console.WriteLine(result.Describe());
                    if (result.Verdict != Detection.Verdict.Undecided) break;
                }
                Console.WriteLine();
            }
            return 0;
        }

        var filter = args.FirstOrDefault();
        var tests = typeof(TestRunner).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
            .Where(m => filter is null || $"{m.DeclaringType!.Name}.{m.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name)
            .ToList();

        var failed = 0;
        foreach (var test in tests)
        {
            var name = $"{test.DeclaringType!.Name}.{test.Name}";
            try
            {
                test.Invoke(null, null);
                Console.WriteLine($"  PASS  {name}");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                failed++;
                Console.WriteLine($"  FAIL  {name}\n        {ex.InnerException.Message.Replace("\n", "\n        ")}");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
        return failed == 0 ? 0 : 1;
    }
}
