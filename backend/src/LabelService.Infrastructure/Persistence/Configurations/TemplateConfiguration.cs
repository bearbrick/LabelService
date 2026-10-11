using LabelService.Domain.Businesses;
using LabelService.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 label_template 的映射：编码唯一，属于一个业务分组，有多个版本。
/// </summary>
internal sealed class TemplateConfiguration : IEntityTypeConfiguration<TemplateEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TemplateEntity> builder)
    {
        builder.ToTable("label_template");
        builder.Property(item => item.Code).HasMaxLength(64);
        builder.Property(item => item.Name).HasMaxLength(128);
        builder.HasOne<BusinessEntity>().WithMany().HasForeignKey(item => item.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Status).HasMaxLength(16);
        builder.Property(item => item.CreatedBy).HasMaxLength(64);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasMany(item => item.Versions)
            .WithOne(version => version.Template)
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
