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
    public async Task<AssetFileDto> GetLogoAsync(CancellationToken cancellationToken = default) =>
        await GetImageAsync(BrandImage.Logo, cancellationToken);

    /// <inheritdoc />
    public Task<SystemInfoDto> UploadLogoAsync(UploadAssetInput? input) => UploadImageAsync(BrandImage.Logo, input);

    /// <inheritdoc />
    public Task<SystemInfoDto> ResetLogoAsync() => ResetImageAsync(BrandImage.Logo);

    /// <inheritdoc />
    public async Task<AssetFileDto> GetIconAsync(CancellationToken cancellationToken = default) =>
        await GetImageAsync(BrandImage.Icon, cancellationToken);

    /// <inheritdoc />
    public Task<SystemInfoDto> UploadIconAsync(UploadAssetInput? input) => UploadImageAsync(BrandImage.Icon, input);

    /// <inheritdoc />
    public Task<SystemInfoDto> ResetIconAsync() => ResetImageAsync(BrandImage.Icon);

    /// <summary>
    /// 取上传的完整 Logo 或图标文件。
    /// </summary>
    /// <param name="image">哪一张图。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图片文件。</returns>
    /// <exception cref="UserFriendlyException">还没上传（NOT_FOUND）。</exception>
    private async Task<AssetFileDto> GetImageAsync(BrandImage image, CancellationToken cancellationToken)
    {
        var fileName = Value(await settings.ListAsync(cancellationToken), image.Key, "");
        return fileName.Length > 0 && files.Find(fileName) is { } file ? file : throw AdminErrors.NotFoundError($"还没有上传{image.Label}");
    }

    /// <summary>
    /// 上传完整 Logo 或图标（PNG、JPG、SVG、WebP，不超过 1 MB），替换原来的。
    /// </summary>
    /// <param name="image">哪一张图。</param>
    /// <param name="input">上传的文件。</param>
    /// <returns>修改后的系统信息。</returns>
    private async Task<SystemInfoDto> UploadImageAsync(BrandImage image, UploadAssetInput? input)
    {
        var (bytes, extension) = await UploadedImages.ReadAsync(input, allowWebp: true);

        // 文件名带内容摘要：换了图地址就变，浏览器不会用缓存里的旧图。
        var fileName = image.Prefix + Convert.ToHexStringLower(SHA256.HashData(bytes))[..12] + extension;
        await files.SaveAsync(fileName, bytes);
        var saved = await settings.ListAsync();
        var old = Value(saved, image.Key, "");
        Set(saved, image.Key, fileName);
        await unitOfWork.SaveChangesAsync();
        if (old.Length > 0 && old != fileName)
        {
            files.Delete(old);
        }

        await operationLog.WriteAsync(LogType, image.Label, $"上传 {extension[1..].ToUpperInvariant()}，{bytes.Length / 1024.0:0.#} KB");
        return ToDto(await settings.ListAsync());
    }

    /// <summary>
    /// 删除上传的完整 Logo 或图标。
    /// </summary>
    /// <param name="image">哪一张图。</param>
    /// <returns>修改后的系统信息。</returns>
    private async Task<SystemInfoDto> ResetImageAsync(BrandImage image)
    {
        var saved = await settings.ListAsync();
        var old = Value(saved, image.Key, "");
        if (old.Length > 0)
        {
            Set(saved, image.Key, "");
            await unitOfWork.SaveChangesAsync();
            files.Delete(old);
            await operationLog.WriteAsync(LogType, image.Label, $"恢复默认（删除上传的{image.Label}）");
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

        // 版本取文件名里的摘要部分，前端拼在图片地址后面；没上传时为 null。
        string? Version(BrandImage image)
        {
            var file = Value(saved, image.Key, "");
            return file.Length == 0 ? null : Path.GetFileNameWithoutExtension(file).Replace(image.Prefix, "", StringComparison.Ordinal);
        }

        return new SystemInfoDto(
            Text(SystemInfoRules.SystemName),
            Text(SystemInfoRules.Subtitle),
            Text(SystemInfoRules.CompanyName),
            Text(SystemInfoRules.Copyright),
            Text(SystemInfoRules.Support),
            Text(SystemInfoRules.LoginTitle),
            Text(SystemInfoRules.LoginIntro),
            Version(BrandImage.Logo),
            Version(BrandImage.Icon));
    }

    /// <summary>
    /// 系统信息里的一张图：完整 Logo 或图标。
    /// </summary>
    /// <param name="Key">存文件名的设置项键。</param>
    /// <param name="Prefix">文件名前缀，后面接内容摘要的前 12 位。</param>
    /// <param name="Label">中文名称，提示和操作日志里用。</param>
    private sealed record BrandImage(string Key, string Prefix, string Label)
    {
        /// <summary>
        /// 完整 Logo：登录页显示；没有图标时顶栏也用它。
        /// </summary>
        public static BrandImage Logo { get; } = new(SystemInfoRules.CompanyLogo, "company-logo-", "公司 Logo");

        /// <summary>
        /// Logo 图标：顶栏和浏览器标签页显示；没有完整 Logo 时登录页也用它。
        /// </summary>
        public static BrandImage Icon { get; } = new(SystemInfoRules.CompanyIcon, "company-icon-", "Logo 图标");
    }
}
