using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LabelService.Infrastructure.Persistence;

/// <summary>
/// 主库和日志库共用的模型约定：时间一律 UTC，列名用下划线风格。
/// </summary>
public static class ModelConventions
{
    /// <summary>
    /// 时间一律存 UTC 的 DateTime（ADR 0003）。
    /// </summary>
    /// <remarks>
    /// SQLite 读回来的 Kind 是 Unspecified，这里标回 Utc；PostgreSQL 的 timestamptz 写入时要求 Kind 是 Utc，这里写入前也统一标成 Utc。
    /// </remarks>
    /// <param name="configurationBuilder">模型约定的配置。</param>
    public static void UseUtcDateTimes(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    /// <summary>
    /// 列名按设计书用下划线风格：WidthMm → width_mm。在 OnModelCreating 最后调用。
    /// </summary>
    /// <param name="modelBuilder">模型。</param>
    public static void UseSnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            property.SetColumnName(SnakeCase(property.Name));
        }
    }

    /// <summary>
    /// 把 PascalCase 的属性名转成 snake_case 的列名。
    /// </summary>
    /// <param name="name">属性名，如 WidthMm。</param>
    /// <returns>列名，如 width_mm。</returns>
    private static string SnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            // 每个大写字母（第一个除外）前面加下划线。
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 时间转换：写入前转成 UTC 并标记 Kind=Utc，读出后标记 Kind=Utc。
    /// </summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => DateTime.SpecifyKind(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
