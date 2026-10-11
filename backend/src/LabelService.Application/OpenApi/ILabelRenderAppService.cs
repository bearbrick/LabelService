using LabelService.Application.ApiClients;
using LabelService.Contracts.Protocol;

namespace LabelService.Application.OpenApi;

/// <summary>
/// 开放接口 /api/v1 的用例：单张生成、批量生成、查模板的字段定义。请求、响应格式见 docs/contracts.md。
/// </summary>
/// <remarks>
/// 在热路径上（AGENTS.md R3）：只用内存缓存（模板、版式），不访问数据库和磁盘。
/// 鉴权（API Key、IP 白名单）和读请求体由接口层做完再调用这里；计时、响应头、指标和调用日志也在接口层。
/// </remarks>
public interface ILabelRenderAppService
{
    /// <summary>
    /// 单张生成（POST /api/v1/render）。
    /// </summary>
    /// <param name="client">已通过鉴权的调用方。</param>
    /// <param name="request">请求体；为 null 时按缺少 templateCode 处理。</param>
    /// <param name="trace">调用记录，处理过程中填写。</param>
    /// <param name="cancellationToken">请求取消（客户端断开）。</param>
    /// <returns>生成的文件，或错误。</returns>
    ValueTask<OpenApiOutcome<RenderedFile>> RenderAsync(AuthenticatedClient client, RenderApiRequest? request, RenderTrace trace, CancellationToken cancellationToken);

    /// <summary>
    /// 批量生成（POST /api/v1/render/batch）。
    /// </summary>
    /// <param name="client">已通过鉴权的调用方。</param>
    /// <param name="request">请求体；为 null 时按缺少 templateCode 处理。</param>
    /// <param name="trace">调用记录，处理过程中填写。</param>
    /// <param name="cancellationToken">请求取消（客户端断开）。</param>
    /// <returns>生成的文件（合并的一个文件、拼版文件或 ZIP），或错误。</returns>
    ValueTask<OpenApiOutcome<RenderedFile>> RenderBatchAsync(AuthenticatedClient client, BatchRenderApiRequest? request, RenderTrace trace, CancellationToken cancellationToken);

    /// <summary>
    /// 模板最新发布版本的字段定义和调用样例（GET /api/v1/templates/{code}/schema）。
    /// </summary>
    /// <param name="client">已通过鉴权的调用方。</param>
    /// <param name="code">模板编码。</param>
    /// <returns>字段定义，或错误（模板不存在、没有权限）。</returns>
    OpenApiOutcome<TemplateSchemaResponse> GetSchema(AuthenticatedClient client, string code);
}
