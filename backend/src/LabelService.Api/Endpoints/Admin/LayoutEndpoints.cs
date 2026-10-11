using LabelService.Application.Layouts;
using LabelService.Application.Layouts.Dtos;
using LabelService.Domain.Identity;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 拼版版式（ADR 0005）：查看登录即可，增删改要权限点 res.manage；预览和打印校准测试页给拼版设计器用。
/// </summary>
internal static class LayoutEndpoints
{
    /// <summary>
    /// 注册路由。
    /// </summary>
    /// <param name="api">/api/admin 路由组。</param>
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/layouts", (double? slotWidth, double? slotHeight, ISheetLayoutAppService service) => service.ListAsync(slotWidth, slotHeight))
            .RequireAdmin();
        api.MapGet("/layouts/{code}", (string code, ISheetLayoutAppService service) => service.GetAsync(code))
            .RequireAdmin();
        api.MapPost("/layouts", (SaveSheetLayoutDto body, ISheetLayoutAppService service) => service.CreateAsync(body))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapPut("/layouts/{code}", (string code, SaveSheetLayoutDto body, ISheetLayoutAppService service) => service.UpdateAsync(code, body))
            .RequireAdmin(PermissionNames.ResourceManage);
        api.MapDelete("/layouts/{code}", async (string code, ISheetLayoutAppService service) =>
        {
            await service.DeleteAsync(code);
            return Results.NoContent();
        }).RequireAdmin(PermissionNames.ResourceManage);
        api.MapPost("/layouts/preview", (SheetLayoutPreviewInput body, HttpContext http, ISheetLayoutAppService service) => RenderedLabelResults.ToResult(http, service.Preview(body)))
            .RequireAdmin(PermissionNames.TemplateView);
        api.MapPost("/layouts/test-page", (SheetLayoutTestPageInput body, HttpContext http, ISheetLayoutAppService service) => RenderedLabelResults.ToResult(http, service.TestPage(body)))
            .RequireAdmin();
    }
}
