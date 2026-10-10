using LabelService.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LabelService.Infrastructure.Persistence.Configurations;

/// <summary>
/// 表 canvas_preset 的映射，建库时写入默认的画布预设。
/// </summary>
internal sealed class CanvasPresetConfiguration : IEntityTypeConfiguration<CanvasPresetEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CanvasPresetEntity> builder)
    {
        builder.ToTable("canvas_preset");
        builder.Property(item => item.Name).HasMaxLength(64);
        builder.Property(item => item.Kind).HasMaxLength(16);
        builder.HasData(DefaultPresets());
    }

    /// <summary>
    /// 建库时写入的画布预设，和设计器的预设列表（frontend/src/model/presets.ts）一致。之后由管理员增删。
    /// </summary>
    /// <returns>八个预设：A4、A5 和六种常用的标签尺寸。</returns>
    private static CanvasPresetEntity[] DefaultPresets()
    {
        (string Name, double Width, double Height)[] presets =
        [
            ("A4", 210, 297), ("A5", 148, 210), ("100×150", 100, 150), ("100×100", 100, 100),
            ("100×60", 100, 60), ("80×50", 80, 50), ("60×40", 60, 40), ("50×30", 50, 30),
        ];
        return presets
            .Select((preset, index) => new CanvasPresetEntity
            {
                Id = index + 1,
                Name = preset.Name,
                WidthMm = preset.Width,
                HeightMm = preset.Height,
                Sort = (index + 1) * 10,
                // 宽度达到 A5（148 mm）的算办公纸张，其余是标签纸。
                Kind = preset.Width >= 148 ? CanvasPresetEntity.OfficePaper : CanvasPresetEntity.LabelPaper,
                Enabled = true,
            })
            .ToArray();
    }
}
