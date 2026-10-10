using LabelService.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 render_log 的映射：后台按时间倒序翻页，常带模板、调用方、结果筛选，每个筛选条件和时间组成复合索引。
/// </summary>
internal sealed class RenderLogConfiguration : IEntityTypeConfiguration<RenderLogEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RenderLogEntity> builder)
    {
        builder.ToTable("render_log");
        builder.Property(item => item.RequestId).HasMaxLength(64);
        builder.Property(item => item.Endpoint).HasMaxLength(16);
        builder.Property(item => item.ClientName).HasMaxLength(64);
        builder.Property(item => item.TemplateCode).HasMaxLength(64);
        builder.Property(item => item.Format).HasMaxLength(8);
        builder.Property(item => item.LayoutCode).HasMaxLength(64);
        builder.Property(item => item.Status).HasMaxLength(16);
        builder.Property(item => item.ErrorCode).HasMaxLength(32);
        builder.HasIndex(item => item.CreatedAt);
        builder.HasIndex(item => new { item.TemplateCode, item.CreatedAt });
        builder.HasIndex(item => new { item.ClientId, item.CreatedAt });
        builder.HasIndex(item => new { item.Status, item.CreatedAt });
        builder.HasIndex(item => item.RequestId);
        builder.Property(item => item.TraceId).HasMaxLength(64);
        builder.Property(item => item.Ip).HasMaxLength(64);
        builder.Property(item => item.ErrorMessage).HasMaxLength(512);
        builder.HasIndex(item => item.TraceId);
    }
}
