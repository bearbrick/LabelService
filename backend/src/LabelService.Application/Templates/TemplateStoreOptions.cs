namespace LabelService.Application.Templates;

/// <summary>
/// 配置节 LabelService:Templates：模板（和拼版版式）从哪里来。
/// </summary>
public sealed class TemplateStoreOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LabelService:Templates";

    /// <summary>
    /// 模板来源，默认数据库。
    /// </summary>
    public TemplateSource Source { get; set; } = TemplateSource.Database;

    /// <summary>
    /// 模板 JSON 目录，相对于程序目录。来源是 Files 时从这里加载；开发环境首次建库时从这里导入示例模板。
    /// </summary>
    public string SeedDirectory { get; set; } = "SeedTemplates";

    /// <summary>
    /// 拼版版式 JSON 目录，相对于程序目录。来源是 Files 时从这里加载；开发环境版式表是空的时候从这里导入示例版式。
    /// </summary>
    public string SeedLayoutDirectory { get; set; } = "SeedLayouts";

    /// <summary>
    /// 模板 JSON 目录的绝对路径。
    /// </summary>
    /// <returns>程序目录下的 <see cref="SeedDirectory"/>。</returns>
    public string ResolveSeedDirectory() => Path.Combine(AppContext.BaseDirectory, SeedDirectory);

    /// <summary>
    /// 版式 JSON 目录的绝对路径。
    /// </summary>
    /// <returns>程序目录下的 <see cref="SeedLayoutDirectory"/>。</returns>
    public string ResolveSeedLayoutDirectory() => Path.Combine(AppContext.BaseDirectory, SeedLayoutDirectory);
}

/// <summary>
/// 模板来源。
/// </summary>
public enum TemplateSource
{
    /// <summary>
    /// 数据库，支持草稿、版本发布、后台管理。
    /// </summary>
    Database,

    /// <summary>
    /// 模板 JSON 目录，只读，版本号固定为 1；没有后台管理和调用日志，适合没有数据目录的只读演示环境。
    /// </summary>
    Files,
}
