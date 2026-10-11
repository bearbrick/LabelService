using LabelService.Domain.Layouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 sheet_layout 的映射：编码唯一。
/// </summary>
internal sealed class SheetLayoutConfiguration : IEntityTypeConfiguration<SheetLayoutEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SheetLayoutEntity> builder)
    {
        builder.ToTable("sheet_layout");
        builder.Property(item => item.Code).HasMaxLength(64);
        builder.Property(item => item.Name).HasMaxLength(128);
        builder.Property(item => item.UpdatedBy).HasMaxLength(64);
        builder.HasIndex(item => item.Code).IsUnique();
    }
}
