using System.Text.Json;
using LabelService.Application.ApiClients;
using LabelService.Application.Diagnostics;
using LabelService.Application.Logs;
using LabelService.Application.OpenApi;
using LabelService.Contracts;
using LabelService.Contracts.Protocol;

namespace LabelService.Api.Endpoints.OpenApi;

/// <summary>
/// 开放接口 /api/v1：业务系统和客户端 SDK 调用。请求、响应格式见 docs/contracts.md。
/// </summary>
/// <remarks>
/// 这里只做 HTTP 的事：识别调用方（API Key、IP 白名单）、读请求体、计时、写响应；生成的规则在 <see cref="ILabelRenderAppService"/>。
/// </remarks>
internal static class PublicApiEndpoints
{
    /// <summary>
    /// 程序缺陷导致渲染失败时给调用方看的说明。
    /// </summary>
    private const string InternalErrorMessage = "渲染内部错误，请凭 traceId 联系标签服务管理员";

    /// <summary>
    /// 注册开放接口路由。
    /// </summary>
    /// <param name="app">路由。</param>
    /// <returns>同一个路由，便于链式调用。</returns>
    public static IEndpointRouteBuilder MapPublicApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPost("/render", RenderAsync);
        api.MapGet("/templates/{code}/schema", GetSchema);
        api.MapPost("/render/batch", RenderBatchAsync);
        return app;
    }

    /// <summary>
    /// POST /api/v1/render：单张生成，成功时直接返回文件流。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="authenticator">API Key 鉴权。</param>
    /// <param name="service">开放接口的用例。</param>
    /// <param name="metrics">指标。</param>
    /// <param name="renderLog">调用日志队列。</param>
    /// <param name="logger">日志。</param>
    /// <returns>文件或错误响应。</returns>
    private static async Task<IResult> RenderAsync(
        HttpContext http,
        ApiKeyAuthenticator authenticator,
        ILabelRenderAppService service,
        LabelMetrics metrics,
        RenderLogQueue renderLog,
        ILogger<RenderCall> logger)
    {
        using var call = new RenderCall(http, metrics, logger, "render", renderLog);
        if (Authenticate(http, authenticator, call) is { } denied)
        {
            return denied;
        }

        var client = call.Authenticated!;

        var (body, readError) = await ReadJsonAsync<RenderApiRequest>(http);
        if (readError is not null)
        {
            return call.Fail(readError);
        }

        try
        {
            var outcome = await service.RenderAsync(client, body, call.Trace, http.RequestAborted);
            return outcome.Value is { } file ? call.Succeed(file) : call.Fail(outcome.Error!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "渲染失败 template={Template} requestId={RequestId}", call.Trace.TemplateCode, call.RequestId);
            return call.Fail(ErrorCodes.RenderFailed, InternalErrorMessage);
        }
    }

    /// <summary>
    /// POST /api/v1/render/batch：批量生成（合并成一个文件、打包 ZIP 或拼版）。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="authenticator">API Key 鉴权。</param>
    /// <param name="service">开放接口的用例。</param>
    /// <param name="metrics">指标。</param>
    /// <param name="renderLog">调用日志队列。</param>
    /// <param name="logger">日志。</param>
    /// <returns>文件或错误响应。</returns>
    private static async Task<IResult> RenderBatchAsync(
        HttpContext http,
        ApiKeyAuthenticator authenticator,
        ILabelRenderAppService service,
        LabelMetrics metrics,
        RenderLogQueue renderLog,
        ILogger<RenderCall> logger)
    {
        using var call = new RenderCall(http, metrics, logger, "batch", renderLog);
        if (Authenticate(http, authenticator, call) is { } denied)
        {
            return denied;
        }

        var client = call.Authenticated!;

        var (body, readError) = await ReadJsonAsync<BatchRenderApiRequest>(http);
        if (readError is not null)
        {
            return call.Fail(readError);
        }

        try
        {
            var outcome = await service.RenderBatchAsync(client, body, call.Trace, http.RequestAborted);
            return outcome.Value is { } file ? call.Succeed(file) : call.Fail(outcome.Error!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "批量渲染失败 template={Template} requestId={RequestId}", call.Trace.TemplateCode, call.RequestId);
            return call.Fail(ErrorCodes.RenderFailed, InternalErrorMessage);
        }
    }

    /// <summary>
    /// GET /api/v1/templates/{code}/schema：最新发布版本的字段定义和调用样例。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="http">当前请求。</param>
    /// <param name="authenticator">API Key 鉴权。</param>
    /// <param name="service">开放接口的用例。</param>
    /// <returns>字段定义或错误响应。</returns>
    private static IResult GetSchema(string code, HttpContext http, ApiKeyAuthenticator authenticator, ILabelRenderAppService service)
    {
        if (CheckClient(http, authenticator, out var client) is { } problem)
        {
            return ApiErrors.Create(http, ErrorCodes.Unauthorized, problem);
        }

        var outcome = service.GetSchema(client!, code);
        return outcome.Value is { } schema
            ? Results.Json(schema, LabelJson.Options)
            : ApiErrors.Create(http, outcome.Error!.Code, outcome.Error.Message);
    }

    /// <summary>
    /// 识别调用方，记下鉴权阶段的耗时和调用方。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="authenticator">API Key 鉴权。</param>
    /// <param name="call">这次调用；识别成功时 <see cref="RenderCall.Authenticated"/> 是调用方。</param>
    /// <returns>识别失败时的错误响应；成功时为 null。</returns>
    private static IResult? Authenticate(HttpContext http, ApiKeyAuthenticator authenticator, RenderCall call)
    {
        var problem = CheckClient(http, authenticator, out var client);
        call.Trace.Mark(TimingStages.Auth);
        if (client is not null)
        {
            call.Client = client.Name;
            call.ClientId = client.Id;
        }

        if (problem is not null)
        {
            return call.Fail(ErrorCodes.Unauthorized, problem);
        }

        call.Authenticated = client;
        return null;
    }

    /// <summary>
    /// 识别调用方：API Key 有效、来源 IP 在白名单里。
    /// </summary>
    /// <param name="http">当前请求。</param>
    /// <param name="authenticator">API Key 鉴权。</param>
    /// <param name="client">Key 对应的调用方；Key 无效时为 null（IP 不在白名单里时不为 null）。</param>
    /// <returns>不通过时给调用方看的说明；通过时为 null。</returns>
    private static string? CheckClient(HttpContext http, ApiKeyAuthenticator authenticator, out AuthenticatedClient? client)
    {
        client = authenticator.Authenticate(http.Request.Headers[HeaderNames.ApiKey]);
        if (client is null)
        {
            return "API Key 缺失或无效";
        }

        return client.AllowsIp(http.Connection.RemoteIpAddress)
            ? null
            : $"来源 IP {http.Connection.RemoteIpAddress} 不在调用方 {client.Name} 的白名单里";
    }

    /// <summary>
    /// 读取 JSON 请求体。格式错误、超过大小上限时返回错误，单张和批量共用。
    /// </summary>
    /// <typeparam name="T">请求体类型。</typeparam>
    /// <param name="http">当前请求。</param>
    /// <returns>请求体（空请求体为 null），或错误。</returns>
    private static async Task<(T? Body, OpenApiError? Error)> ReadJsonAsync<T>(HttpContext http)
        where T : class
    {
        try
        {
            return (await JsonSerializer.DeserializeAsync<T>(http.Request.Body, LabelJson.Options, http.RequestAborted), null);
        }
        catch (JsonException ex)
        {
            return (null, new OpenApiError(ErrorCodes.InvalidJson, $"请求体不是合法 JSON：{ex.Message}", StatusCode: 400));
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, new OpenApiError(ErrorCodes.InvalidJson, $"请求体超过 {RenderLimits.MaxRequestBodyBytes / 1024 / 1024} MB", StatusCode: 413));
        }
    }
}
