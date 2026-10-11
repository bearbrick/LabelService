using LabelService.Domain.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 system_setting 的映射：键是主键。建库时不写数据，没保存过的项用 <see cref="SystemInfoRules"/> 里的默认值。
/// </summary>
internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSettingEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SystemSettingEntity> builder)
    {
        builder.ToTable("system_setting");
        builder.HasKey(item => item.Key);
        builder.Property(item => item.Key).HasMaxLength(64);
        builder.Property(item => item.Value).HasMaxLength(500);
    }
}
