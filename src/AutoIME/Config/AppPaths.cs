namespace AutoIME.Config;

/// <summary>保存場所はすべて %LOCALAPPDATA%\AutoIME\ 配下 (設計書 §21)。ネットワークには何も送らない。</summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoIME");

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");
    public static string ModelFile => Path.Combine(DataDirectory, "model.json");
    public static string ConversionHistoryFile => Path.Combine(DataDirectory, "conversions.json");
    public static string UserDictionaryFile => Path.Combine(DataDirectory, "userdict.txt");
    public static string LogFile => Path.Combine(DataDirectory, "autoime.log");

    /// <summary>ユーザー辞書 (japanese.txt / english.txt) を置くと組み込み辞書に追加される。</summary>
    public static string UserDictionaryDirectory => Path.Combine(DataDirectory, "dictionaries");
}
