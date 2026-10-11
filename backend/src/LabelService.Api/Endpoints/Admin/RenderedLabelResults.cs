using System.Globalization;
using LabelService.Application.Rendering.Dtos;
using LabelService.Contracts.Protocol;

namespace LabelService.Api.Endpoints.Admin;

/// <summary>
/// 把管理接口里渲染出来的文件写成响应：文件内容加上 X-Label-* 和 Server-Timing 响应头（值为 null 的不写）。
/// </summary>
internal static class RenderedLabelResults
{
    /// <summary>
    /// 写响应头并返回文件。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="file">渲染出来的文件。</param>
    /// <returns>文件响应；有文件名时带 Content-Disposition。</returns>
    public static IResult ToResult(HttpContext http, RenderedLabelDto file)
    {
        var headers = http.Response.Headers;
        if (file.TemplateVersion is { } version)
        {
            headers[HeaderNames.TemplateVersion] = version.ToString(CultureInfo.InvariantCulture);
        }

        if (file.LabelCount is { } labels)
        {
            headers[HeaderNames.LabelCount] = labels.ToString(CultureInfo.InvariantCulture);
        }

        if (file.PageCount is { } pages)
        {
            headers[HeaderNames.PageCount] = pages.ToString(CultureInfo.InvariantCulture);
        }

        if (file.Dpi is { } dpi)
        {
            headers[HeaderNames.LabelDpi] = dpi.ToString(CultureInfo.InvariantCulture);
        }

        headers[HeaderNames.ServerTiming] = file.ServerTiming;
        if (file.Warnings.Count > 0)
        {
            headers[HeaderNames.LabelWarnings] = string.Join(",", file.Warnings);
        }

        return file.FileName is null ? Results.Bytes(file.Content, file.ContentType) : Results.Bytes(file.Content, file.ContentType, file.FileName);
    }
}
