using LabelService.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 business 的映射：编码唯一。
/// </summary>
internal sealed class BusinessConfiguration : IEntityTypeConfiguration<BusinessEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BusinessEntity> builder)
    {
        builder.ToTable("business");
        builder.Property(item => item.Code).HasMaxLength(32);
        builder.Property(item => item.Name).HasMaxLength(64);
        builder.Property(item => item.Description).HasMaxLength(256);
        builder.Property(item => item.Color).HasMaxLength(16);
        builder.HasIndex(item => item.Code).IsUnique();
    }
}
