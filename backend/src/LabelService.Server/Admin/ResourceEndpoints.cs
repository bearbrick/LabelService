using System.Security.Cryptography;
using LabelService.Contracts.Protocol;
using LabelService.Domain.ApiClients;
using LabelService.Domain.Businesses;
using LabelService.Domain.Identity;
using LabelService.Domain.Layouts;
using LabelService.Domain.Logs;
using LabelService.Domain.Resources;
using LabelService.Domain.Shared;
using LabelService.Domain.Templates;
using LabelService.Infrastructure.Persistence;
using LabelService.Rendering.Assets;
using LabelService.Rendering.Fonts;
using LabelService.Server.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LabelService.Server.Admin;

/// <summary>
/// 画布预设、素材、字体。查看登录即可（设计器要用），增删改要 res.manage。
/// </summary>
/// <remarks>
/// 素材上传：PNG、JPG、SVG，不超过 1 MB；按文件内容判断类型（不信扩展名），SVG 先清洗再保存。
/// 文件存到数据目录的 assets/（文件名就是素材 Key），然后重新加载素材库，模板里写 asset:Key 立即可用。
/// </remarks>
internal static class ResourceEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/presets", ListPresetsAsync).RequireAdmin();
        api.MapPost("/presets", CreatePresetAsync).RequireAdmin(PermissionNames.ResourceManage);
        api.MapPut("/presets/{id:int}", UpdatePresetAsync).RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/presets/{id:int}", DeletePresetAsync).RequireAdmin(PermissionNames.ResourceManage);

        api.MapGet("/assets", ListAssetsAsync).RequireAdmin();
        api.MapGet("/assets/{key}/file", GetAssetFile).RequireAdmin();
        api.MapPost("/assets", UploadAssetAsync).RequireAdmin(PermissionNames.ResourceManage).DisableAntiforgery();
        api.MapPut("/assets/{key}", RenameAssetAsync).RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/assets/{key}", DeleteAssetAsync).RequireAdmin(PermissionNames.ResourceManage);

        api.MapGet("/fonts", (FontRegistry fonts) => Results.Json(new
        {
            families = fonts.FamilyNames.Order(StringComparer.Ordinal).ToArray(),
            files = fonts.Files.Select(Path.GetFileName).ToArray(),
        })).RequireAdmin();
    }

    private static async Task<IResult> ListPresetsAsync(IDbContextFactory<LabelDbContext> contexts)
    {
        await using var db = await contexts.CreateDbContextAsync();
        return Results.Json(await db.CanvasPresets.AsNoTracking().OrderBy(item => item.Sort).ThenBy(item => item.Id).ToListAsync());
    }

    private static async Task<IResult> CreatePresetAsync(PresetRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var preset = new CanvasPresetEntity { Sort = (await db.CanvasPresets.MaxAsync(item => (int?)item.Sort) ?? 0) + 10 };
        if (Apply(preset, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        db.CanvasPresets.Add(preset);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "画布预设", preset.Name, $"新建 {preset.WidthMm}×{preset.HeightMm} mm");
        return Results.Json(preset);
    }

    private static async Task<IResult> UpdatePresetAsync(int id, PresetRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var preset = await db.CanvasPresets.FindAsync(id);
        if (preset is null)
        {
            return AdminResults.NotFound("画布预设不存在");
        }

        if (Apply(preset, body) is { } problem)
        {
            return AdminResults.Invalid(problem);
        }

        await db.SaveChangesAsync();
        await log.WriteAsync(http, "画布预设", preset.Name, $"修改为 {preset.WidthMm}×{preset.HeightMm} mm");
        return Results.Json(preset);
    }

    private static async Task<IResult> DeletePresetAsync(int id, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var preset = await db.CanvasPresets.FindAsync(id);
        if (preset is null)
        {
            return AdminResults.NotFound("画布预设不存在");
        }

        // 选中预设后宽高已经写进模板，删预设不影响已有模板。
        db.CanvasPresets.Remove(preset);
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "画布预设", preset.Name, "删除");
        return Results.NoContent();
    }

    private static string? Apply(CanvasPresetEntity preset, PresetRequest body)
    {
        var name = (body.Name ?? "").Trim();
        if (name.Length is 0 or > 64)
        {
            return "名称不能为空，最长 64 字";
        }

        if (body.Width is not (> 0 and <= 1000) || body.Height is not (> 0 and <= 1000))
        {
            return "宽高要在 0–1000 mm 之间";
        }

        preset.Name = name;
        preset.Kind = string.IsNullOrWhiteSpace(body.Kind) ? "标签纸" : body.Kind.Trim();
        preset.WidthMm = Math.Round(body.Width.Value, 2);
        preset.HeightMm = Math.Round(body.Height.Value, 2);
        preset.Enabled = body.Enabled ?? true;
        return null;
    }

    /// <summary>
    /// 素材列表：内置素材（程序目录 images/）和上传的素材（表里有记录）。
    /// </summary>
    private static async Task<IResult> ListAssetsAsync(IDbContextFactory<LabelDbContext> contexts, AssetLibrary library)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var uploaded = await db.Assets.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync();
        var keys = uploaded.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var builtin = library.ImageIds.Where(id => !keys.Contains(id)).Order(StringComparer.Ordinal)
            .Select(id => new { key = id, name = id, builtin = true, size = 0L, createdAt = (DateTime?)null });
        return Results.Json(builtin.Concat(uploaded.Select(item => new { key = item.Key, name = item.Name, builtin = false, size = item.Size, createdAt = (DateTime?)item.CreatedAt })));
    }

    /// <summary>
    /// 素材文件，设计器显示用。先找上传目录，再找内置目录。
    /// </summary>
    private static IResult GetAssetFile(string key, IOptions<DataOptions> data, IOptions<AssetOptions> assets)
    {
        if (!AssetKeyIsSafe(key))
        {
            return AdminResults.NotFound("素材不存在");
        }

        foreach (var directory in new[] { Path.Combine(data.Value.ResolveDirectory(), "assets"), assets.Value.ResolveImageDirectory() })
        {
            foreach (var (extension, type) in new[] { (".png", "image/png"), (".jpg", "image/jpeg"), (".svg", "image/svg+xml") })
            {
                var path = Path.Combine(directory, key + extension);
                if (File.Exists(path))
                {
                    // SVG 都清洗过，再加一层 CSP，防止直接打开时执行任何东西。
                    return Results.File(File.ReadAllBytes(path), type, enableRangeProcessing: false, lastModified: File.GetLastWriteTimeUtc(path))
                        .WithHeaders(("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'"), ("X-Content-Type-Options", "nosniff"));
                }
            }
        }

        return AdminResults.NotFound("素材不存在");
    }

    private static async Task<IResult> UploadAssetAsync(IFormFile? file, HttpContext http, IDbContextFactory<LabelDbContext> contexts, IOptions<DataOptions> data, AssetLibrary library, OperationLog log, TimeProvider clock)
    {
        if (file is null || file.Length == 0)
        {
            return AdminResults.Invalid("请选择图片文件");
        }

        if (file.Length > RenderLimits.MaxImageBytes)
        {
            return AdminResults.Invalid("图片超过 1 MB，请压缩后再上传");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        string extension;
        if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]))
        {
            extension = ".png";
        }
        else if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            extension = ".jpg";
        }
        else
        {
            try
            {
                bytes = System.Text.Encoding.UTF8.GetBytes(SvgSanitizer.Sanitize(System.Text.Encoding.UTF8.GetString(bytes)));
                extension = ".svg";
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
            {
                return AdminResults.Invalid("只支持 PNG、JPG、SVG 图片");
            }
        }

        if (extension != ".svg")
        {
            using var decoded = SkiaSharp.SKImage.FromEncodedData(bytes);
            if (decoded is null)
            {
                return AdminResults.Invalid("图片无法解码，文件可能损坏");
            }
        }

        var key = "a" + RandomNumberGenerator.GetHexString(10, lowercase: true);
        var directory = Path.Combine(data.Value.ResolveDirectory(), "assets");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, key + extension), bytes);
        var name = Path.GetFileNameWithoutExtension(file.FileName);
        var record = new AssetEntity
        {
            Key = key,
            Kind = "image",
            Name = string.IsNullOrWhiteSpace(name) ? key : name.Length > 128 ? name[..128] : name,
            FilePath = key + extension,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Size = bytes.Length,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        await using (var db = await contexts.CreateDbContextAsync())
        {
            db.Assets.Add(record);
            await db.SaveChangesAsync();
        }

        library.ReloadImages();
        await log.WriteAsync(http, "素材", record.Name, $"上传 {extension[1..].ToUpperInvariant()}，{bytes.Length / 1024.0:0.#} KB，模板里写 asset:{key}");
        return Results.Json(new
        {
            key,
            name = record.Name,
            builtin = false,
            size = record.Size,
            createdAt = record.CreatedAt
        });
    }

    private static async Task<IResult> RenameAssetAsync(string key, RenameRequest body, HttpContext http, IDbContextFactory<LabelDbContext> contexts, OperationLog log)
    {
        var name = (body.Name ?? "").Trim();
        if (name.Length is 0 or > 128)
        {
            return AdminResults.Invalid("名称不能为空，最长 128 字");
        }

        await using var db = await contexts.CreateDbContextAsync();
        var asset = await db.Assets.SingleOrDefaultAsync(item => item.Key == key);
        if (asset is null)
        {
            return AdminResults.NotFound("素材不存在（内置素材不能改名）");
        }

        var old = asset.Name;
        asset.Name = name;
        await db.SaveChangesAsync();
        await log.WriteAsync(http, "素材", name, $"由“{old}”改名");
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAssetAsync(string key, HttpContext http, IDbContextFactory<LabelDbContext> contexts, IOptions<DataOptions> data, AssetLibrary library, OperationLog log)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var asset = await db.Assets.SingleOrDefaultAsync(item => item.Key == key);
        if (asset is null)
        {
            return AdminResults.NotFound("素材不存在（内置素材不能删除）");
        }

        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
        var path = Path.Combine(data.Value.ResolveDirectory(), "assets", asset.FilePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        library.ReloadImages();
        await log.WriteAsync(http, "素材", asset.Name, $"删除（引用 asset:{key} 的模板生成时会报 IMAGE_INVALID）");
        return Results.NoContent();
    }

    private static bool AssetKeyIsSafe(string key) => key.Length is > 0 and <= 32 && key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-');

    private static IResult WithHeaders(this IResult result, params (string Name, string Value)[] headers) => new HeaderResult(result, headers);

    /// <summary>预设：名称、类型、宽高（mm）、是否启用。</summary>
    internal sealed record PresetRequest(string? Name, string? Kind, double? Width, double? Height, bool? Enabled);

    /// <summary>素材改名。</summary>
    internal sealed record RenameRequest(string? Name);

    /// <summary>
    /// 给结果加响应头。
    /// </summary>
    private sealed class HeaderResult(IResult inner, (string Name, string Value)[] headers) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            foreach (var (name, value) in headers)
            {
                httpContext.Response.Headers[name] = value;
            }

            return inner.ExecuteAsync(httpContext);
        }
    }
}
