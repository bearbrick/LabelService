using Microsoft.Data.Sqlite;

namespace LabelService.Server.Data;

/// <summary>
/// 配置节 LabelService:Data：数据目录（ADR 0003）。
/// </summary>
internal sealed class DataOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Data";

    /// <summary>
    /// 主库文件名：画布预设、模板、版本、素材元数据。
    /// </summary>
    public const string DatabaseFileName = "label.db";

    /// <summary>
    /// 数据目录。相对路径相对于程序目录，默认程序目录下的 data/。不能放网络共享盘，也不能让两个服务进程共用。
    /// </summary>
    public string Directory { get; set; } = "data";

    /// <summary>
    /// 数据目录的绝对路径。
    /// </summary>
    public string ResolveDirectory() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, Directory));

    /// <summary>
    /// 主库的连接字符串。
    /// </summary>
    public string ConnectionString() =>
        new SqliteConnectionStringBuilder { DataSource = Path.Combine(ResolveDirectory(), DatabaseFileName) }.ToString();
}
