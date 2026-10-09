using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LabelService.Server.Data;

/// <summary>
/// 调用日志库 label-log.db（ADR 0003）：和主库分开，日志批量写入不会和后台保存模板抢写锁。迁移在 Data/LogMigrations。
/// </summary>
internal sealed class LogDbContext(DbContextOptions<LogDbContext> options) : DbContext(options)
{
    /// <summary>调用日志。</summary>
    public DbSet<RenderLogRecord> RenderLogs => Set<RenderLogRecord>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        LabelDbContext.UseUtcDateTimes(configurationBuilder);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RenderLogRecord>(log =>
        {
            log.ToTable("render_log");
            log.Property(item => item.RequestId).HasMaxLength(64);
            log.Property(item => item.Endpoint).HasMaxLength(16);
            log.Property(item => item.ClientName).HasMaxLength(64);
            log.Property(item => item.TemplateCode).HasMaxLength(64);
            log.Property(item => item.Format).HasMaxLength(8);
            log.Property(item => item.LayoutCode).HasMaxLength(64);
            log.Property(item => item.Status).HasMaxLength(16);
            log.Property(item => item.ErrorCode).HasMaxLength(32);

            // 后台按时间倒序翻页，常带模板、调用方、结果筛选：每个筛选条件和时间组成复合索引。
            log.HasIndex(item => item.CreatedAt);
            log.HasIndex(item => new { item.TemplateCode, item.CreatedAt });
            log.HasIndex(item => new { item.ClientId, item.CreatedAt });
            log.HasIndex(item => new { item.Status, item.CreatedAt });
            log.HasIndex(item => item.RequestId);
        });

        LabelDbContext.UseSnakeCaseColumns(modelBuilder);
    }
}

/// <summary>
/// 表 render_log：一次生成请求一条。不存业务数据。
/// </summary>
internal sealed class RenderLogRecord
{
    /// <summary>主键。</summary>
    public long Id
    {
        get; set;
    }

    /// <summary>调用方传的 X-Request-Id；没传时是服务端的链路 ID。</summary>
    public string RequestId { get; set; } = "";

    /// <summary>接口：render、batch、preview。</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>调用方 ID（api_client.id）；配置文件里的调用方和未识别的请求为 null。</summary>
    public int? ClientId
    {
        get; set;
    }

    /// <summary>调用方名称，未识别时为 anonymous。调用方改名、删除后仍能看出是谁。</summary>
    public string ClientName { get; set; } = "";

    /// <summary>模板编码。</summary>
    public string TemplateCode { get; set; } = "";

    /// <summary>实际使用的模板版本；没走到取模板时为 null。</summary>
    public int? VersionNo
    {
        get; set;
    }

    /// <summary>输出格式：pdf、png、zpl。</summary>
    public string Format { get; set; } = "";

    /// <summary>拼版版式编码（T-018）；不拼版时为 null。</summary>
    public string? LayoutCode
    {
        get; set;
    }

    /// <summary>张数（含份数），失败时为 0。</summary>
    public int ItemCount
    {
        get; set;
    }

    /// <summary>服务端总耗时，毫秒。</summary>
    public double DurationMs
    {
        get; set;
    }

    /// <summary>各阶段耗时，如 {"auth":0.3,"layout":4.2}。</summary>
    public string StageMsJson { get; set; } = "{}";

    /// <summary>是否慢调用（平均每张超过 100 ms）。</summary>
    public bool IsSlow
    {
        get; set;
    }

    /// <summary>结果：ok、error、aborted。</summary>
    public string Status { get; set; } = "";

    /// <summary>失败时的错误码。</summary>
    public string? ErrorCode
    {
        get; set;
    }

    /// <summary>请求时间（UTC）。</summary>
    public DateTime CreatedAt
    {
        get; set;
    }
}

/// <summary>
/// 给 dotnet ef 生成日志库迁移用，运行时不用。
/// </summary>
internal sealed class LogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LogDbContext>
{
    /// <inheritdoc />
    public LogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LogDbContext>().UseSqlite("Data Source=design-time-log.db").Options);
}
