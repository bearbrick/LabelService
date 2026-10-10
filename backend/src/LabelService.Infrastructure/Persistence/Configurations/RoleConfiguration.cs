using LabelService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 app_role 的映射：名称唯一。
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("app_role");
        builder.Property(item => item.Name).HasMaxLength(64);
        builder.Property(item => item.Description).HasMaxLength(256);
        builder.Property(item => item.Permissions).HasMaxLength(1024);
        builder.HasIndex(item => item.Name).IsUnique();
    }
}
