using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LabelService.Server.Data;

/// <summary>
/// 主库 label.db：画布预设、模板、版本、素材元数据、调用方。表结构见设计书第 3 节，用迁移管理（Data/Migrations）。
/// </summary>
/// <remarks>
/// 只给管理操作和启动加载用。开放接口从 <see cref="Templates.DbTemplateStore"/> 的内存缓存取模板，不访问数据库（R3）。
/// 只用 EF Core 的通用能力，不写 SQLite 特有的 SQL（ADR 0003），以后换数据库只需换 Provider、重新生成迁移。
/// </remarks>
internal sealed class LabelDbContext(DbContextOptions<LabelDbContext> options) : DbContext(options)
{
    /// <summary>画布预设。</summary>
    public DbSet<CanvasPresetRecord> CanvasPresets => Set<CanvasPresetRecord>();

    /// <summary>模板。</summary>
    public DbSet<TemplateRecord> Templates => Set<TemplateRecord>();

    /// <summary>模板版本，包括草稿。</summary>
    public DbSet<TemplateVersionRecord> TemplateVersions => Set<TemplateVersionRecord>();

    /// <summary>素材元数据。</summary>
    public DbSet<AssetRecord> Assets => Set<AssetRecord>();

    /// <summary>调用方。</summary>
    public DbSet<ApiClientRecord> ApiClients => Set<ApiClientRecord>();

    /// <summary>业务分组。</summary>
    public DbSet<BusinessRecord> Businesses => Set<BusinessRecord>();

    /// <summary>后台用户。</summary>
    public DbSet<UserRecord> Users => Set<UserRecord>();

    /// <summary>角色。</summary>
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();

    /// <summary>拼版版式。</summary>
    public DbSet<SheetLayoutRecord> SheetLayouts => Set<SheetLayoutRecord>();

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardPublishedVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardPublishedVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) => UseUtcDateTimes(configurationBuilder);

    /// <summary>
    /// 时间一律存 UTC 的 DateTime（ADR 0003）。SQLite 读回来的 Kind 是 Unspecified，这里标回 Utc。主库和日志库共用。
    /// </summary>
    internal static void UseUtcDateTimes(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    /// <summary>
    /// 列名按设计书用下划线风格：WidthMm → width_mm。主库和日志库共用，放在 OnModelCreating 最后调用。
    /// </summary>
    internal static void UseSnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            property.SetColumnName(SnakeCase(property.Name));
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CanvasPresetRecord>(preset =>
        {
            preset.ToTable("canvas_preset");
            preset.Property(item => item.Name).HasMaxLength(64);
            preset.Property(item => item.Kind).HasMaxLength(16);
            preset.HasData(DefaultPresets());
        });

        modelBuilder.Entity<TemplateRecord>(template =>
        {
            template.ToTable("label_template");
            template.Property(item => item.Code).HasMaxLength(64);
            template.Property(item => item.Name).HasMaxLength(128);
            template.HasOne<BusinessRecord>().WithMany().HasForeignKey(item => item.BusinessId).OnDelete(DeleteBehavior.Restrict);
            template.Property(item => item.Status).HasMaxLength(16);
            template.Property(item => item.CreatedBy).HasMaxLength(64);
            template.HasIndex(item => item.Code).IsUnique();
            template.HasMany(item => item.Versions)
                .WithOne(version => version.Template)
                .HasForeignKey(version => version.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TemplateVersionRecord>(version =>
        {
            version.ToTable("label_template_version");
            version.Property(item => item.Status).HasMaxLength(16).IsConcurrencyToken();
            version.Property(item => item.PublishedBy).HasMaxLength(64);
            version.HasIndex(item => new { item.TemplateId, item.VersionNo }).IsUnique();
        });

        modelBuilder.Entity<AssetRecord>(asset =>
        {
            asset.ToTable("asset");
            asset.Property(item => item.Kind).HasMaxLength(16);
            asset.Property(item => item.Key).HasMaxLength(32);
            asset.HasIndex(item => item.Key).IsUnique();
            asset.Property(item => item.Name).HasMaxLength(128);
            asset.Property(item => item.FilePath).HasMaxLength(256);
            asset.Property(item => item.Sha256).HasMaxLength(64);
            asset.HasIndex(item => item.Sha256);
        });

        modelBuilder.Entity<ApiClientRecord>(client =>
        {
            client.ToTable("api_client");
            client.Property(item => item.Name).HasMaxLength(64);
            client.Property(item => item.ApiKeyHash).HasMaxLength(64);
            client.Property(item => item.KeyPrefix).HasMaxLength(16);
            client.Property(item => item.AppId).HasMaxLength(64);
            client.Property(item => item.Description).HasMaxLength(256);
            client.Property(item => item.BusinessIds).HasMaxLength(1024);
            client.Property(item => item.IpAllowlist).HasMaxLength(1024);
            client.HasIndex(item => item.Name).IsUnique();
            client.HasIndex(item => item.ApiKeyHash).IsUnique();
        });

        modelBuilder.Entity<BusinessRecord>(business =>
        {
            business.ToTable("business");
            business.Property(item => item.Code).HasMaxLength(32);
            business.Property(item => item.Name).HasMaxLength(64);
            business.Property(item => item.Description).HasMaxLength(256);
            business.Property(item => item.Color).HasMaxLength(16);
            business.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<UserRecord>(user =>
        {
            user.ToTable("app_user");
            user.Property(item => item.Account).HasMaxLength(64);
            user.Property(item => item.Name).HasMaxLength(64);
            user.Property(item => item.Phone).HasMaxLength(32);
            user.Property(item => item.PasswordHash).HasMaxLength(256);
            user.Property(item => item.BusinessIds).HasMaxLength(1024);
            user.Property(item => item.Status).HasMaxLength(16);
            user.HasIndex(item => item.Account).IsUnique();
            user.HasOne<RoleRecord>().WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleRecord>(role =>
        {
            role.ToTable("app_role");
            role.Property(item => item.Name).HasMaxLength(64);
            role.Property(item => item.Description).HasMaxLength(256);
            role.Property(item => item.Permissions).HasMaxLength(1024);
            role.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<SheetLayoutRecord>(layout =>
        {
            layout.ToTable("sheet_layout");
            layout.Property(item => item.Code).HasMaxLength(64);
            layout.Property(item => item.Name).HasMaxLength(128);
            layout.Property(item => item.UpdatedBy).HasMaxLength(64);
            layout.HasIndex(item => item.Code).IsUnique();
        });

        UseSnakeCaseColumns(modelBuilder);
    }

    /// <summary>
    /// 建库时写入的画布预设，和设计器的预设列表（frontend/src/model/presets.ts）一致。之后由管理员增删。
    /// </summary>
    private static CanvasPresetRecord[] DefaultPresets()
    {
        (string Name, double Width, double Height)[] presets =
        [
            ("A4", 210, 297), ("A5", 148, 210), ("100×150", 100, 150), ("100×100", 100, 100),
            ("100×60", 100, 60), ("80×50", 80, 50), ("60×40", 60, 40), ("50×30", 50, 30),
        ];
        return presets
            .Select((preset, index) => new CanvasPresetRecord
            {
                Id = index + 1,
                Name = preset.Name,
                WidthMm = preset.Width,
                HeightMm = preset.Height,
                Sort = (index + 1) * 10,
                Kind = preset.Width >= 148 ? "办公纸张" : "标签纸",
                Enabled = true,
            })
            .ToArray();
    }

    private void GuardPublishedVersions()
    {
        foreach (var entry in ChangeTracker.Entries<TemplateVersionRecord>())
        {
            if ((entry.State is EntityState.Modified or EntityState.Deleted)
                && entry.OriginalValues.GetValue<string>(nameof(TemplateVersionRecord.Status)) == TemplateStatuses.Published)
            {
                throw new InvalidOperationException($"模板版本 {entry.Entity.VersionNo} 已发布，不能修改或删除");
            }
        }
    }

    private static string SnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
