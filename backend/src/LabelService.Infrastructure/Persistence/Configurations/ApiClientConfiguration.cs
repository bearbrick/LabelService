using LabelService.Domain.ApiClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 api_client 的映射：名称和 Key 哈希都唯一。
/// </summary>
internal sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClientEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApiClientEntity> builder)
    {
        builder.ToTable("api_client");
        builder.Property(item => item.Name).HasMaxLength(64);
        builder.Property(item => item.ApiKeyHash).HasMaxLength(64);
        builder.Property(item => item.KeyPrefix).HasMaxLength(16);
        builder.Property(item => item.AppId).HasMaxLength(64);
        builder.Property(item => item.Description).HasMaxLength(256);
        builder.Property(item => item.BusinessIds).HasMaxLength(1024);
        builder.Property(item => item.IpAllowlist).HasMaxLength(1024);
        builder.HasIndex(item => item.Name).IsUnique();
        builder.HasIndex(item => item.ApiKeyHash).IsUnique();
    }
}
