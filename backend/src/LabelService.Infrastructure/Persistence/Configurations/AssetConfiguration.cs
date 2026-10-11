using LabelService.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 asset 的映射：素材 Key 唯一，按内容哈希建索引。
/// </summary>
internal sealed class AssetConfiguration : IEntityTypeConfiguration<AssetEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssetEntity> builder)
    {
        builder.ToTable("asset");
        builder.Property(item => item.Kind).HasMaxLength(16);
        builder.Property(item => item.Key).HasMaxLength(32);
        builder.HasIndex(item => item.Key).IsUnique();
        builder.Property(item => item.Name).HasMaxLength(128);
        builder.Property(item => item.FilePath).HasMaxLength(256);
        builder.Property(item => item.Sha256).HasMaxLength(64);
        builder.HasIndex(item => item.Sha256);
    }
}
