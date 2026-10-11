using LabelService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 app_user 的映射：账号唯一，属于一个角色（角色有人在用时不能删）。
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("app_user");
        builder.Property(item => item.Account).HasMaxLength(64);
        builder.Property(item => item.Name).HasMaxLength(64);
        builder.Property(item => item.Phone).HasMaxLength(32);
        builder.Property(item => item.PasswordHash).HasMaxLength(256);
        builder.Property(item => item.BusinessIds).HasMaxLength(1024);
        builder.Property(item => item.Status).HasMaxLength(16);
        builder.HasIndex(item => item.Account).IsUnique();
        builder.HasOne<RoleEntity>().WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
