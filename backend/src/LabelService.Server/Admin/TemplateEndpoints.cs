using System.Collections.Concurrent;
using LabelService.Contracts.Templates;
using LabelService.Server.Templates;

namespace LabelService.Server.Admin;

/// <summary>
/// 模板管理：列表、打开、新建、保存草稿、发布、版本、回滚、换分组、删除、发布前检查。
/// </summary>
/// <remarks>
/// 每个操作同时检查权限点和业务范围；不在范围里的模板当作不存在（404），不暴露是否存在。
/// 保存草稿很频繁（设计器自动保存），操作日志同一个人同一个模板 10 分钟只记一条。
/// </remarks>
internal static class TemplateEndpoints
{
    private static readonly ConcurrentDictionary<(int User, string Code), DateTime> LastDraftLog = new();

    /// <summary>
    /// 注册路由。
    /// </summary>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/templates", ListAsync).RequireAdmin(Permissions.TemplateView);
        api.MapPost("/templates", CreateAsync).RequireAdmin(Permissions.TemplateEdit);
        api.MapPost("/templates/check", Check).RequireAdmin(Permissions.TemplateView);
        api.MapGet("/templates/{code}", GetAsync).RequireAdmin(Permissions.TemplateView);
        api.MapGet("/templates/{code}/thumbnail", ThumbnailAsync).RequireAdmin(Permissions.TemplateView);
        api.MapDelete("/templates/{code}", DeleteAsync).RequireAdmin(Permissions.TemplateDelete);
        api.MapPut("/templates/{code}/draft", SaveDraftAsync).RequireAdmin(Permissions.TemplateEdit);
        api.MapDelete("/templates/{code}/draft", DiscardDraftAsync).RequireAdmin(Permissions.TemplateEdit);
        api.MapPost("/templates/{code}/publish", PublishAsync).RequireAdmin(Permissions.TemplatePublish);
        api.MapGet("/templates/{code}/versions", ListVersionsAsync).RequireAdmin(Permissions.TemplateView);
        api.MapGet("/templates/{code}/versions/{version:int}", GetVersionAsync).RequireAdmin(Permissions.TemplateView);
        api.MapPost("/templates/{code}/versions/{version:int}/restore", RestoreAsync).RequireAdmin(Permissions.TemplateEdit);
        api.MapPut("/templates/{code}/business", MoveAsync).RequireAdmin(Permissions.TemplateEdit);
    }

    /// <summary>
    /// 模板库列表：概要加上画布、DPI、字段和元素个数。
    /// </summary>
    private static async Task<IResult> ListAsync(HttpContext http, TemplateRepository repository)
    {
        var admin = http.CurrentAdmin();
        var templates = await repository.ListWithOutlineAsync();
        return Results.Json(templates.Where(item => admin.InScope(item.Summary.BusinessId)).Select(item => new
        {
            id = item.Summary.Id,
            code = item.Summary.Code,
            name = item.Summary.Name,
            businessId = item.Summary.BusinessId,
            status = item.Summary.Status,
            latestVersion = item.Summary.LatestVersion,
            hasDraft = item.Summary.HasDraft,
            createdBy = item.Summary.CreatedBy,
            updatedBy = item.Summary.UpdatedBy,
            updatedAt = item.Summary.UpdatedAt,
            width = item.Outline.Width,
            height = item.Outline.Height,
            dpi = item.Outline.Dpi,
            fieldCount = item.Outline.FieldCount,
            elementCount = item.Outline.ElementCount,
        }));
    }

    /// <summary>
    /// 缩略图（PNG），渲染失败时 204，前端显示占位图。
    /// </summary>
    private static async Task<IResult> ThumbnailAsync(string code, HttpContext http, TemplateRepository repository, TemplatePreviews previews)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        var content = await repository.GetDraftAsync(code) ?? (template.LatestVersion > 0 ? await repository.GetVersionAsync(code, template.LatestVersion) : null);
        var png = content is null ? null : previews.Thumbnail(code, template.UpdatedAt, content);
        if (png is null)
        {
            return Results.NoContent();
        }

        http.Response.Headers.CacheControl = "private, max-age=60";
        return Results.File(png, "image/png");
    }

    /// <summary>
    /// 打开模板：有草稿时给草稿，没有时给最新发布版本的内容。
    /// </summary>
    private static async Task<IResult> GetAsync(string code, HttpContext http, TemplateRepository repository)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        var draft = await repository.GetDraftAsync(code);
        var content = draft ?? (template.LatestVersion > 0 ? await repository.GetVersionAsync(code, template.LatestVersion) : null);
        return Results.Json(new
        {
            code = template.Code,
            name = template.Name,
            businessId = template.BusinessId,
            status = template.Status,
            latestVersion = template.LatestVersion,
            hasDraft = draft is not null,
            updatedBy = template.UpdatedBy,
            updatedAt = template.UpdatedAt,
            content,
        });
    }

    private static async Task<IResult> CreateAsync(CreateRequest body, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var admin = http.CurrentAdmin();
        if (!admin.InScope(body.BusinessId))
        {
            return AdminResults.Forbidden("不能在这个业务分组里新建模板");
        }

        var code = (body.Code ?? "").Trim();
        if (await repository.FindAsync(code) is not null)
        {
            return AdminResults.Invalid($"模板编码 {code} 已存在");
        }

        var content = body.Content ?? new LabelTemplate { Page = new LabelPage { Width = body.Width ?? 100, Height = body.Height ?? 60, Dpi = body.Dpi ?? 300 } };
        content.Code = code;
        content.Name = string.IsNullOrWhiteSpace(body.Name) ? content.Name : body.Name.Trim();
        try
        {
            await repository.SaveDraftAsync(content, admin.DisplayName, body.BusinessId);
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        await log.WriteAsync(http, "新建模板", $"{content.Code} {content.Name}", "新建模板");
        return Results.Json(new
        {
            code = content.Code
        });
    }

    private static async Task<IResult> SaveDraftAsync(string code, LabelTemplate body, HttpContext http, TemplateRepository repository, OperationLog log, TimeProvider clock)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        if (body.Code != code)
        {
            return AdminResults.Invalid("模板编码不能在设计器里修改");
        }

        int version;
        try
        {
            version = await repository.SaveDraftAsync(body, http.CurrentAdmin().DisplayName);
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        var admin = http.CurrentAdmin();
        var now = clock.GetUtcNow().UtcDateTime;
        if (!LastDraftLog.TryGetValue((admin.User.Id, code), out var last) || now - last > TimeSpan.FromMinutes(10))
        {
            LastDraftLog[(admin.User.Id, code)] = now;
            await log.WriteAsync(http, "编辑模板", $"{code} {body.Name}", "保存草稿");
        }

        return Results.Json(new
        {
            draftVersion = version,
            savedAt = now
        });
    }

    private static async Task<IResult> DiscardDraftAsync(string code, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        try
        {
            if (!await repository.DiscardDraftAsync(code))
            {
                return AdminResults.Invalid("没有未发布的草稿");
            }
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        await log.WriteAsync(http, "编辑模板", $"{code} {template.Name}", "放弃未发布的改动");
        return Results.NoContent();
    }

    private static async Task<IResult> PublishAsync(string code, PublishRequest? body, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        PublishResult result;
        try
        {
            result = await repository.PublishAsync(code, http.CurrentAdmin().DisplayName, body?.Note ?? "");
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        var note = string.IsNullOrWhiteSpace(body?.Note) ? "" : "：" + body.Note.Trim();
        await log.WriteAsync(http, "发布模板", $"{code} {template.Name}", $"发布 v{result.Version}{note}");
        return Results.Json(result);
    }

    private static async Task<IResult> ListVersionsAsync(string code, HttpContext http, TemplateRepository repository) =>
        await FindInScopeAsync(code, http, repository) is null
            ? AdminResults.NotFound($"模板 {code} 不存在")
            : Results.Json(await repository.ListVersionsAsync(code));

    private static async Task<IResult> GetVersionAsync(string code, int version, HttpContext http, TemplateRepository repository)
    {
        if (await FindInScopeAsync(code, http, repository) is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        var content = await repository.GetVersionAsync(code, version);
        return content is null ? AdminResults.NotFound($"模板 {code} 没有已发布的版本 {version}") : Results.Json(content);
    }

    private static async Task<IResult> RestoreAsync(string code, int version, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        try
        {
            var draft = await repository.RestoreToDraftAsync(code, version, http.CurrentAdmin().DisplayName);
            await log.WriteAsync(http, "回滚版本", $"{code} {template.Name}", $"把 v{version} 的内容恢复成草稿（发布后成为 v{draft}）");
            return Results.Json(new
            {
                draftVersion = draft
            });
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }
    }

    private static async Task<IResult> MoveAsync(string code, MoveRequest body, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        if (!http.CurrentAdmin().InScope(body.BusinessId))
        {
            return AdminResults.Forbidden("不能把模板移到自己范围以外的业务分组");
        }

        try
        {
            await repository.MoveAsync(code, body.BusinessId, http.CurrentAdmin().DisplayName);
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        await log.WriteAsync(http, "编辑模板", $"{code} {template.Name}", "换了业务分组");
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(string code, HttpContext http, TemplateRepository repository, OperationLog log)
    {
        var template = await FindInScopeAsync(code, http, repository);
        if (template is null)
        {
            return AdminResults.NotFound($"模板 {code} 不存在");
        }

        try
        {
            await repository.DeleteAsync(code, http.CurrentAdmin().DisplayName);
        }
        catch (ManagementException ex)
        {
            return AdminResults.From(ex);
        }

        await log.WriteAsync(http, "删除模板", $"{code} {template.Name}", "删除模板，开放接口不能再调用");
        return Results.NoContent();
    }

    /// <summary>
    /// 发布前检查（不保存）：静态规则 + 试渲染计时。设计器的“检查”按钮和发布对话框用。
    /// </summary>
    private static IResult Check(LabelTemplate body, TemplatePublishValidator validator) => Results.Json(validator.Check(body));

    private static async Task<Data.TemplateRecord?> FindInScopeAsync(string code, HttpContext http, TemplateRepository repository)
    {
        var template = await repository.FindAsync(code);
        return template is not null && http.CurrentAdmin().InScope(template.BusinessId) ? template : null;
    }

    /// <summary>新建模板：给内容，或者只给尺寸（空白模板）。</summary>
    internal sealed record CreateRequest(string? Code, string? Name, int? BusinessId, double? Width, double? Height, int? Dpi, LabelTemplate? Content);

    /// <summary>发布：可以带发布说明。</summary>
    internal sealed record PublishRequest(string? Note);

    /// <summary>换业务分组。</summary>
    internal sealed record MoveRequest(int? BusinessId);
}
