// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

using Meltype.Config;

namespace Meltype.Tests;

/// <summary>プロファイル (仕事用・趣味用・SNS 用など): 設定の値をまとめて切り替える。</summary>
internal static class ProfileTests
{
    [Test]
    public static void Profiles_SwitchValuesAndKeepSharedOnes()
    {
        var settings = new Settings().Normalize();
        Assert.Equal("標準", settings.ActiveProfile, "最初は「標準」だけ");
        Assert.Equal(1, settings.Profiles.Count);

        // 「仕事用」を足す (今の値を写す) → 仕事用だけ慎重・ライブ変換 OFF にする
        var work = settings.AddProfile("仕事用")!;
        Assert.Equal("仕事用", work.ActiveProfile);
        work.DetectionLevel = DetectionLevel.Conservative;
        work.LiveConversion = false;
        work.AppKinds.Add(new AppKind { Name = "チャット" });

        // 標準に戻すと、標準の値に戻る。共通の項目 (ログ・更新) は切り替えても変わらない
        work.FileLog = true;
        var standard = work.SwitchProfile("標準");
        Assert.Equal("標準", standard.ActiveProfile);
        Assert.Equal(DetectionLevel.Balanced, standard.DetectionLevel);
        Assert.True(standard.LiveConversion, "標準はライブ変換 ON のまま");
        Assert.Equal(0, standard.AppKinds.Count, "表 (独自の種類) もプロファイルごと");
        Assert.True(standard.FileLog, "ログはどのプロファイルでも共通");

        // もう一度 仕事用 にすると、仕事用で変えた値が戻ってくる
        var back = standard.SwitchProfile("仕事用");
        Assert.Equal(DetectionLevel.Conservative, back.DetectionLevel);
        Assert.True(!back.LiveConversion, "仕事用はライブ変換 OFF");
        Assert.Equal("チャット", back.AppKinds.Single().Name);

        // 保存して読み直しても同じ
        var path = Path.Combine(Path.GetTempPath(), $"meltype-profile-{Environment.ProcessId}.json");
        try
        {
            back.Save(path);
            var loaded = Settings.Load(path);
            Assert.Equal("仕事用", loaded.ActiveProfile);
            Assert.Equal(DetectionLevel.Balanced, loaded.SwitchProfile("標準").DetectionLevel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public static void Profiles_RenameAndRemove()
    {
        var settings = new Settings().Normalize().AddProfile("SNS")!;
        Assert.True(settings.AddProfile("SNS") is null, "同じ名前は足せない");
        Assert.True(settings.AddProfile("  ") is null, "空の名前は足せない");

        var renamed = settings.RenameProfile("SNS", "SNS 用")!;
        Assert.Equal("SNS 用", renamed.ActiveProfile, "使っているものの名前を変えたら、使っている名前も変わる");

        // 使っているものを消すと、残りの最初のものに切り替わる。最後の 1 つは消せない
        var removed = renamed.RemoveProfile("SNS 用")!;
        Assert.Equal("標準", removed.ActiveProfile);
        Assert.Equal(1, removed.Profiles.Count);
        Assert.True(removed.RemoveProfile("標準") is null, "最後の 1 つは消せない");
    }
}
