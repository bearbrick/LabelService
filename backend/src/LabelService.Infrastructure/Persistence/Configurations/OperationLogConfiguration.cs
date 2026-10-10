using LabelService.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 op_log 的映射：按时间、操作人、操作类型筛选。
/// </summary>
internal sealed class OperationLogConfiguration : IEntityTypeConfiguration<OperationLogEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OperationLogEntity> builder)
    {
        builder.ToTable("op_log");
        builder.Property(item => item.UserName).HasMaxLength(64);
        builder.Property(item => item.Type).HasMaxLength(32);
        builder.Property(item => item.Target).HasMaxLength(256);
        builder.Property(item => item.Detail).HasMaxLength(1024);
        builder.Property(item => item.Ip).HasMaxLength(64);
        builder.HasIndex(item => item.CreatedAt);
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.HasIndex(item => new { item.Type, item.CreatedAt });
    }
}
