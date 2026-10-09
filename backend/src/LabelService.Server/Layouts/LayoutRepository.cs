using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabelService.Server.Layouts;

/// <summary>
/// 版式的数据库读写（表 sheet_layout）。改动保存后立即更新 <see cref="LayoutStore"/>，开放接口马上能用。
/// </summary>
internal sealed class LayoutRepository(IDbContextFactory<LabelDbContext> contexts, LayoutStore store, TimeProvider time)
{
    /// <summary>
    /// 把全部启用的版式加载进内存快照，返回个数。启动时调用一次。
    /// </summary>
    public async Task<int> LoadAsync()
    {
        await using var db = await contexts.CreateDbContextAsync();
        var records = await db.SheetLayouts.AsNoTracking().Where(item => item.Enabled).ToListAsync();
        store.Load(records.Select(ToModel));
        return records.Count;
    }

    /// <summary>
    /// 全部版式（含停用的），按编码排序。
    /// </summary>
    public async Task<List<SheetLayoutRecord>> ListAsync()
    {
        await using var db = await contexts.CreateDbContextAsync();
        return await db.SheetLayouts.AsNoTracking().OrderBy(item => item.Code).ToListAsync();
    }

    /// <summary>
    /// 按编码取版式记录，不存在时返回 null。
    /// </summary>
    public async Task<SheetLayoutRecord?> FindAsync(string code)
    {
        await using var db = await contexts.CreateDbContextAsync();
        return await db.SheetLayouts.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code);
    }

    /// <summary>
    /// 新建或修改版式。长度按 0.1 mm 取整后再检查；检查不通过抛 <see cref="ManagementException"/>。
    /// </summary>
    /// <param name="layout">版式内容。修改时编码不能变。</param>
    /// <param name="enabled">是否启用。</param>
    /// <param name="account">操作人账号。</param>
    /// <param name="create">true 新建（编码已存在时报错），false 修改（不存在时返回 null）。</param>
    public async Task<SheetLayoutRecord?> SaveAsync(SheetLayout layout, bool enabled, string account, bool create)
    {
        var normalized = Normalize(layout);
        var problems = LayoutValidator.Validate(normalized);
        if (problems.Count > 0)
        {
            throw new ManagementException("版式检查不通过", problems);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var record = await db.SheetLayouts.SingleOrDefaultAsync(item => item.Code == normalized.Code);
        if (create && record is not null)
        {
            throw new ManagementException($"版式编码 {normalized.Code} 已存在");
        }

        if (!create && record is null)
        {
            return null;
        }

        if (record is null)
        {
            record = new SheetLayoutRecord { Code = normalized.Code };
            db.SheetLayouts.Add(record);
        }

        Apply(record, normalized);
        record.Enabled = enabled;
        record.UpdatedBy = account;
        record.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        store.Set(normalized, enabled);
        return record;
    }

    /// <summary>
    /// 删除版式，不存在时返回 false。已生成的文件不受影响，之后带这个编码的请求返回 LAYOUT_NOT_FOUND。
    /// </summary>
    public async Task<bool> DeleteAsync(string code)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var record = await db.SheetLayouts.SingleOrDefaultAsync(item => item.Code == code);
        if (record is null)
        {
            return false;
        }

        db.SheetLayouts.Remove(record);
        await db.SaveChangesAsync();
        store.Remove(code);
        return true;
    }

    /// <summary>
    /// 表记录转成版式 JSON 模型。
    /// </summary>
    public static SheetLayout ToModel(SheetLayoutRecord record) => new()
    {
        Code = record.Code,
        Name = record.Name,
        Paper = new SheetSize { Width = record.PaperWidthMm, Height = record.PaperHeightMm },
        Slot = new SheetSize { Width = record.SlotWidthMm, Height = record.SlotHeightMm },
        Slots = JsonSerializer.Deserialize<List<SheetSlot>>(record.SlotsJson, LabelJson.Options) ?? [],
        Offset = new SheetOffset { X = record.OffsetXMm, Y = record.OffsetYMm },
        CutLines = record.CutLines,
    };

    /// <summary>
    /// 长度按 0.1 mm 取整，名称去掉首尾空白；返回新对象，不改传入的版式。
    /// </summary>
    public static SheetLayout Normalize(SheetLayout layout) => new()
    {
        Code = (layout.Code ?? "").Trim(),
        Name = (layout.Name ?? "").Trim(),
        Paper = layout.Paper is null ? null! : new SheetSize { Width = Round(layout.Paper.Width), Height = Round(layout.Paper.Height) },
        Slot = layout.Slot is null ? null! : new SheetSize { Width = Round(layout.Slot.Width), Height = Round(layout.Slot.Height) },
        Slots = layout.Slots?.Select(slot => slot is null ? null! : new SheetSlot { X = Round(slot.X), Y = Round(slot.Y), Rotation = slot.Rotation }).ToList() ?? null!,
        Offset = layout.Offset is null ? new SheetOffset() : new SheetOffset { X = Round(layout.Offset.X), Y = Round(layout.Offset.Y) },
        CutLines = layout.CutLines,
    };

    private static void Apply(SheetLayoutRecord record, SheetLayout layout)
    {
        record.Name = layout.Name;
        record.PaperWidthMm = layout.Paper.Width;
        record.PaperHeightMm = layout.Paper.Height;
        record.SlotWidthMm = layout.Slot.Width;
        record.SlotHeightMm = layout.Slot.Height;
        record.SlotsJson = JsonSerializer.Serialize(layout.Slots, LabelJson.Options);
        record.OffsetXMm = layout.Offset.X;
        record.OffsetYMm = layout.Offset.Y;
        record.CutLines = layout.CutLines;
    }

    private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, 1, MidpointRounding.AwayFromZero) : value;
}
