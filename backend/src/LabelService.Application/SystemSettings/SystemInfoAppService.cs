using System.Security.Cryptography;
using LabelService.Application.Logs;
using LabelService.Application.Resources;
using LabelService.Application.Resources.Dtos;
using LabelService.Application.Shared;
using LabelService.Application.SystemSettings.Dtos;
using LabelService.Domain.Shared;
using LabelService.Domain.SystemSettings;

namespace LabelService.Application.SystemSettings;

/// <summary>
/// 系统信息的应用服务实现。文字设置存在 system_setting 表，Logo 文件存在数据目录的 branding/，表里只记文件名。
/// </summary>
/// <param name="settings">系统设置仓储。</param>
/// <param name="unitOfWork">工作单元。</param>
/// <param name="files">Logo 文件的存放。</param>
/// <param name="operationLog">操作日志。</param>
/// <param name="clock">时间来源。</param>
public sealed class SystemInfoAppService(
    ISystemSettingRepository settings,
    IUnitOfWork unitOfWork,
    IBrandingFileStore files,
    IOperationLogger operationLog,
    TimeProvider clock) : ISystemInfoAppService
{
    /// <summary>
    /// Logo 文件名的前缀，后面接内容摘要的前 12 位。
    /// </summary>
    private const string LogoPrefix = "company-logo-";

    /// <summary>
    /// 操作日志里的类型。
    /// </summary>
    private const string LogType = "系统信息";

    /// <inheritdoc />
    public async Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default) =>
        ToDto(await settings.ListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<SystemInfoDto> SaveAsync(SaveSystemInfoDto? input)
    {
        if (input is null)
        {
            throw new UserFriendlyException("请填写系统信息");
        }

        // 先全部校验，有一项不合格就整体不保存；没传的项（null）不改。
        var values = SystemInfoRules.Fields
            .Where(field => Pick(input, field.Key) is not null)
            .ToDictionary(field => field, field => SystemInfoRules.Normalize(field, Pick(input, field.Key)));
        var saved = await settings.ListAsync();
        var changed = new List<string>();
        foreach (var (field, value) in values)
        {
            var entity = saved.FirstOrDefault(item => item.Key == field.Key);
            if ((entity?.Value ?? field.Default) == value)
            {
                continue;
            }

            Set(saved, field.Key, value);
            changed.Add(field.Label);
        }

        if (changed.Count > 0)
        {
            await unitOfWork.SaveChangesAsync();
            var name = Value(await settings.ListAsync(), SystemInfoRules.SystemName, SystemInfoRules.Fields[0].Default);
            await operationLog.WriteAsync(LogType, name, "修改" + string.Join("、", changed));
        }

        return ToDto(await settings.ListAsync());
    }

    /// <inheritdoc />
    public async Task<AssetFileDto> GetLogoAsync(CancellationToken cancellationToken = default)
    {
        var fileName = Value(await settings.ListAsync(cancellationToken), SystemInfoRules.CompanyLogo, "");
        return fileName.Length > 0 && files.Find(fileName) is { } file ? file : throw AdminErrors.NotFoundError("还没有上传公司 Logo");
    }

    /// <inheritdoc />
    public async Task<SystemInfoDto> UploadLogoAsync(UploadAssetInput? input)
    {
        var (bytes, extension) = await UploadedImages.ReadAsync(input);

        // 文件名带内容摘要：换了图地址就变，浏览器不会用缓存里的旧图。
        var fileName = LogoPrefix + Convert.ToHexStringLower(SHA256.HashData(bytes))[..12] + extension;
        await files.SaveAsync(fileName, bytes);
        var saved = await settings.ListAsync();
        var old = Value(saved, SystemInfoRules.CompanyLogo, "");
        Set(saved, SystemInfoRules.CompanyLogo, fileName);
        await unitOfWork.SaveChangesAsync();
        if (old.Length > 0 && old != fileName)
        {
            files.Delete(old);
        }

        await operationLog.WriteAsync(LogType, "公司 Logo", $"上传 {extension[1..].ToUpperInvariant()}，{bytes.Length / 1024.0:0.#} KB");
        return ToDto(await settings.ListAsync());
    }

    /// <inheritdoc />
    public async Task<SystemInfoDto> ResetLogoAsync()
    {
        var saved = await settings.ListAsync();
        var old = Value(saved, SystemInfoRules.CompanyLogo, "");
        if (old.Length > 0)
        {
            Set(saved, SystemInfoRules.CompanyLogo, "");
            await unitOfWork.SaveChangesAsync();
            files.Delete(old);
            await operationLog.WriteAsync(LogType, "公司 Logo", "恢复成占位（删除上传的 Logo）");
        }

        return ToDto(await settings.ListAsync());
    }

    /// <summary>
    /// 取表单里某一项的值。
    /// </summary>
    /// <param name="input">表单。</param>
    /// <param name="key">设置项的键。</param>
    /// <returns>用户填的值。</returns>
    private static string? Pick(SaveSystemInfoDto input, string key) => key switch
    {
        SystemInfoRules.SystemName => input.SystemName,
        SystemInfoRules.Subtitle => input.Subtitle,
        SystemInfoRules.CompanyName => input.CompanyName,
        SystemInfoRules.Copyright => input.Copyright,
        SystemInfoRules.Support => input.Support,
        SystemInfoRules.LoginTitle => input.LoginTitle,
        SystemInfoRules.LoginIntro => input.LoginIntro,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知的设置项"),
    };

    /// <summary>
    /// 已保存的值；没保存过时用默认值。
    /// </summary>
    /// <param name="saved">已保存的设置项。</param>
    /// <param name="key">设置项的键。</param>
    /// <param name="fallback">默认值。</param>
    /// <returns>值。</returns>
    private static string Value(IReadOnlyList<SystemSettingEntity> saved, string key, string fallback) =>
        saved.FirstOrDefault(item => item.Key == key)?.Value ?? fallback;

    /// <summary>
    /// 改一项的值：已有就改，没有就新增。调用方负责保存。
    /// </summary>
    /// <param name="saved">已保存的设置项（跟踪中）。</param>
    /// <param name="key">设置项的键。</param>
    /// <param name="value">新值。</param>
    private void Set(IReadOnlyList<SystemSettingEntity> saved, string key, string value)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (saved.FirstOrDefault(item => item.Key == key) is { } entity)
        {
            entity.Value = value;
            entity.UpdatedAt = now;
            return;
        }

        settings.Add(new SystemSettingEntity { Key = key, Value = value, UpdatedAt = now });
    }

    /// <summary>
    /// 转成接口返回的系统信息。
    /// </summary>
    /// <param name="saved">已保存的设置项。</param>
    /// <returns>系统信息。</returns>
    private static SystemInfoDto ToDto(IReadOnlyList<SystemSettingEntity> saved)
    {
        string Text(string key) => Value(saved, key, SystemInfoRules.Fields.First(field => field.Key == key).Default);
        var logo = Value(saved, SystemInfoRules.CompanyLogo, "");

        // 版本取文件名里的摘要部分，前端拼在 Logo 地址后面。
        var version = logo.Length == 0 ? null : Path.GetFileNameWithoutExtension(logo).Replace(LogoPrefix, "", StringComparison.Ordinal);
        return new SystemInfoDto(
            Text(SystemInfoRules.SystemName),
            Text(SystemInfoRules.Subtitle),
            Text(SystemInfoRules.CompanyName),
            Text(SystemInfoRules.Copyright),
            Text(SystemInfoRules.Support),
            Text(SystemInfoRules.LoginTitle),
            Text(SystemInfoRules.LoginIntro),
            version);
    }
}
