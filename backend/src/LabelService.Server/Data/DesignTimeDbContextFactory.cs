using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LabelService.Server.Data;

/// <summary>
/// 给 dotnet ef 生成迁移用，运行时不用。生成迁移的命令见 backend/README.md。
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LabelDbContext>
{
    /// <inheritdoc />
    public LabelDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LabelDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
