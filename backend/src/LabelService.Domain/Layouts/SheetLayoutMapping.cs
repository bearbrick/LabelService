using System.Text.Json;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;

namespace LabelService.Domain.Layouts;

/// <summary>
/// 版式实体（表里的一行）和版式 JSON 模型（docs/contracts.md“拼版”）之间的转换。
/// </summary>
public static class SheetLayoutMapping
{
    /// <summary>
    /// 实体转成版式 JSON 模型。
    /// </summary>
    /// <param name="entity">表里的一行。</param>
    /// <returns>版式模型，格子顺序就是填充顺序。</returns>
    public static SheetLayout ToLayout(SheetLayoutEntity entity) => new()
    {
        Code = entity.Code,
        Name = entity.Name,
        Paper = new SheetSize { Width = entity.PaperWidthMm, Height = entity.PaperHeightMm },
        Slot = new SheetSize { Width = entity.SlotWidthMm, Height = entity.SlotHeightMm },
        Slots = JsonSerializer.Deserialize<List<SheetSlot>>(entity.SlotsJson, LabelJson.Options) ?? [],
        Offset = new SheetOffset { X = entity.OffsetXMm, Y = entity.OffsetYMm },
        CutLines = entity.CutLines,
    };

    /// <summary>
    /// 把版式 JSON 模型的内容写进实体（编码、启用状态、修改记录不在这里写）。
    /// </summary>
    /// <param name="entity">要写入的实体。</param>
    /// <param name="layout">已经取整、检查通过的版式。</param>
    public static void Apply(SheetLayoutEntity entity, SheetLayout layout)
    {
        entity.Name = layout.Name;
        entity.PaperWidthMm = layout.Paper.Width;
        entity.PaperHeightMm = layout.Paper.Height;
        entity.SlotWidthMm = layout.Slot.Width;
        entity.SlotHeightMm = layout.Slot.Height;
        entity.SlotsJson = JsonSerializer.Serialize(layout.Slots, LabelJson.Options);
        entity.OffsetXMm = layout.Offset.X;
        entity.OffsetYMm = layout.Offset.Y;
        entity.CutLines = layout.CutLines;
    }
}
