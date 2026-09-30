// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

namespace Meltype.Tests;

/// <summary>
/// OS に依存しない部分 (Meltype.Core) のテスト。Windows 以外 (Mac・Linux) でも動く。
///   dotnet run --project src/Meltype.Core.Tests                 … すべてのテスト
///   dotnet run --project src/Meltype.Core.Tests -- Composition  … 名前に一致するテストだけ
///   dotnet run --project src/Meltype.Core.Tests -- --eval       … 品質テストの正解率と外れた例
///   dotnet run --project src/Meltype.Core.Tests -- --explain konnichiwa hello … 1 文字ずつの判定理由
/// Windows では src/Meltype.Tests から実行すると、Windows のスペルチェッカーを使い、Windows 専用のテストもまとめて流す。
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        switch (args.FirstOrDefault())
        {
            case "--eval":
                Quality.Print(Quality.Run());
                return 0;
            case "--explain":
                TestHost.Explain(args.Skip(1));
                return 0;
            default:
                return TestHost.Run([typeof(Program).Assembly], args.FirstOrDefault());
        }
    }
}
