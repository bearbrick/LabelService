using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace LabelService.Backend.Checks;

/// <summary>
/// 在进程内启动真实服务：开发环境配置、指定的数据目录，只输出警告以上的日志。
/// </summary>
/// <remarks>
/// 内容根目录要显式指定：WebApplicationFactory 默认按 .sln 推断，不认识 .slnx。
/// </remarks>
internal sealed class ServerFactory(string backendRoot, string dataDirectory, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var values = new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            ["LabelService:Data:Directory"] = dataDirectory,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        builder.UseContentRoot(Path.Combine(backendRoot, "src", "LabelService.Api"))
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
    }
}

/// <summary>
/// 检查程序用的临时数据目录：每次新建，跑完删除（ADR 0003）。
/// </summary>
internal static class TempDataDirectory
{
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "label-service-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 删除目录。连接池里的连接还占着库文件（Windows 上删不掉），先清空连接池。
    /// </summary>
    public static void Delete(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
