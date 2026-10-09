namespace LabelService.Benchmarks;

/// <summary>
/// 基准用到的目录。BenchmarkDotNet 在 bin 下的子目录里运行，从程序目录往上找到 backend 目录。
/// </summary>
internal static class BenchmarkPaths
{
    /// <summary>
    /// backend 目录（有 LabelService.Backend.slnx 的那一层）。
    /// </summary>
    public static string BackendDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LabelService.Backend.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到 LabelService.Backend.slnx");
    }

    /// <summary>
    /// 字体目录 backend/assets/fonts。
    /// </summary>
    public static string FontDirectory() => Path.Combine(BackendDirectory(), "assets", "fonts");

    /// <summary>
    /// 基线文件 backend/tests/LabelService.Benchmarks/baseline.json。
    /// </summary>
    public static string BaselineFile() => Path.Combine(BackendDirectory(), "tests", "LabelService.Benchmarks", "baseline.json");
}
