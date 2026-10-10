using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Resources;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 主库：画布预设、模板和版本、素材元数据、调用方、业务分组、后台用户和角色、拼版版式。表结构见设计书第 3 节。
/// </summary>
/// <remarks>
/// <para>
/// 模型只有这一份，每种数据库一个派生类（<c>SqliteLabelDbContext</c> 等），各自一套迁移（ADR 0010）。
/// 只用 EF Core 的通用能力，不写某种数据库特有的 SQL。
/// </para>
/// <para>
/// 只给管理操作和启动加载用。开放接口从应用层的内存缓存取模板，不访问数据库（AGENTS.md R3）。
/// </para>
/// </remarks>
public abstract class LabelDbContext : DbContext
{
    /// <summary>
    /// 由派生类传入具体数据库的配置。
    /// </summary>
    /// <param name="options">数据库连接和提供程序的配置。</param>
    protected LabelDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// 画布预设。
    /// </summary>
    public DbSet<CanvasPresetEntity> CanvasPresets => Set<CanvasPresetEntity>();

    /// <summary>
    /// 模板。
    /// </summary>
    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();

    /// <summary>
    /// 模板版本，包括草稿。
    /// </summary>
    public DbSet<TemplateVersionEntity> TemplateVersions => Set<TemplateVersionEntity>();

    /// <summary>
    /// 上传的素材的元数据。
    /// </summary>
    public DbSet<AssetEntity> Assets => Set<AssetEntity>();

    /// <summary>
    /// 调用方。
    /// </summary>
    public DbSet<ApiClientEntity> ApiClients => Set<ApiClientEntity>();

    /// <summary>
    /// 业务分组。
    /// </summary>
    public DbSet<BusinessEntity> Businesses => Set<BusinessEntity>();

    /// <summary>
    /// 后台用户。
    /// </summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>
    /// 角色。
    /// </summary>
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    /// <summary>
    /// 拼版版式。
    /// </summary>
    public DbSet<SheetLayoutEntity> SheetLayouts => Set<SheetLayoutEntity>();

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
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        ModelConventions.UseUtcDateTimes(configurationBuilder);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CanvasPresetConfiguration());
        modelBuilder.ApplyConfiguration(new TemplateConfiguration());
        modelBuilder.ApplyConfiguration(new TemplateVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AssetConfiguration());
        modelBuilder.ApplyConfiguration(new ApiClientConfiguration());
        modelBuilder.ApplyConfiguration(new BusinessConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new SheetLayoutConfiguration());

        // 列名统一成下划线风格，放在最后，覆盖全部实体。
        ModelConventions.UseSnakeCaseColumns(modelBuilder);
    }

    /// <summary>
    /// 发布后的版本不能修改、不能删除：保存前检查，发现就抛异常（程序缺陷，不是操作者的问题）。
    /// </summary>
    private void GuardPublishedVersions()
    {
        foreach (var entry in ChangeTracker.Entries<TemplateVersionEntity>())
        {
            // 用数据库里原来的状态判断：草稿刚改成已发布的那一次保存是允许的。
            if ((entry.State is EntityState.Modified or EntityState.Deleted)
                && entry.OriginalValues.GetValue<string>(nameof(TemplateVersionEntity.Status)) == TemplateStatuses.Published)
            {
                throw new InvalidOperationException($"模板版本 {entry.Entity.VersionNo} 已发布，不能修改或删除");
            }
        }
    }
}
