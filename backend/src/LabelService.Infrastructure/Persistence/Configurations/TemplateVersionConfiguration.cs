using LabelService.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 label_template_version 的映射：(template_id, version_no) 唯一，状态是并发令牌。
/// </summary>
internal sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TemplateVersionEntity> builder)
    {
        builder.ToTable("label_template_version");
        builder.Property(item => item.Status).HasMaxLength(16).IsConcurrencyToken();
        builder.Property(item => item.PublishedBy).HasMaxLength(64);
        builder.HasIndex(item => new { item.TemplateId, item.VersionNo }).IsUnique();
    }
}
