using LabelService.Application.Logs;
using LabelService.Application.Resources.Dtos;
using LabelService.Application.Shared;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;

namespace LabelService.Application.Resources;

/// <summary>
/// 画布预设的应用服务实现。
/// </summary>
/// <param name="presets">画布预设仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="operationLog">操作日志。</param>
public sealed class CanvasPresetAppService(ICanvasPresetRepository presets, IUnitOfWork unitOfWork, IOperationLogger operationLog) : ICanvasPresetAppService
{
    /// <summary>
    /// 宽高的上限，mm。
    /// </summary>
    private const double MaxSizeMm = 1000;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CanvasPresetDto>> ListAsync(CancellationToken cancellationToken = default) =>
        (await presets.ListAsync(cancellationToken)).Select(ToDto).ToList();

    /// <inheritdoc />
    public async Task<CanvasPresetDto> CreateAsync(SaveCanvasPresetDto input)
    {
        // 新预设排在最后：排序号按 10 递增。
        var preset = new CanvasPresetEntity { Sort = await presets.MaxSortAsync() + 10 };
        Apply(preset, input);
        presets.Add(preset);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("画布预设", preset.Name, $"新建 {preset.WidthMm}×{preset.HeightMm} mm");
        return ToDto(preset);
    }

    /// <inheritdoc />
    public async Task<CanvasPresetDto> UpdateAsync(int id, SaveCanvasPresetDto input)
    {
        var preset = await presets.FindAsync(id) ?? throw AdminErrors.NotFoundError("画布预设不存在");
        Apply(preset, input);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("画布预设", preset.Name, $"修改为 {preset.WidthMm}×{preset.HeightMm} mm");
        return ToDto(preset);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        var preset = await presets.FindAsync(id) ?? throw AdminErrors.NotFoundError("画布预设不存在");
        presets.Remove(preset);
        await unitOfWork.SaveChangesAsync();
        await operationLog.WriteAsync("画布预设", preset.Name, "删除");
    }

    /// <summary>
    /// 转成接口返回的预设。
    /// </summary>
    /// <param name="preset">预设实体。</param>
    /// <returns>预设 DTO。</returns>
    private static CanvasPresetDto ToDto(CanvasPresetEntity preset) =>
        new(preset.Id, preset.Name, preset.Kind, preset.WidthMm, preset.HeightMm, preset.Sort, preset.Enabled);

    /// <summary>
    /// 校验并写入名称、类型、宽高和启用状态（新建、修改共用）。
    /// </summary>
    /// <param name="preset">要写入的预设。</param>
    /// <param name="input">请求内容。</param>
    /// <exception cref="UserFriendlyException">名称为空或太长、宽高超出范围。</exception>
    private static void Apply(CanvasPresetEntity preset, SaveCanvasPresetDto input)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length is 0 or > 64)
        {
            throw new UserFriendlyException("名称不能为空，最长 64 字");
        }

        if (input.Width is not (> 0 and <= MaxSizeMm) || input.Height is not (> 0 and <= MaxSizeMm))
        {
            throw new UserFriendlyException("宽高要在 0–1000 mm 之间");
        }

        preset.Name = name;
        preset.Kind = string.IsNullOrWhiteSpace(input.Kind) ? CanvasPresetEntity.LabelPaper : input.Kind.Trim();
        preset.WidthMm = Math.Round(input.Width.Value, 2);
        preset.HeightMm = Math.Round(input.Height.Value, 2);
        preset.Enabled = input.Enabled ?? true;
    }
}
