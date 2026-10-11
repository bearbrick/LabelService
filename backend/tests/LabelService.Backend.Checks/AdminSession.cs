using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查程序里调管理接口用的会话：登录拿令牌，之后每个请求带 Authorization: Bearer。
/// </summary>
/// <param name="http">调用服务的客户端。</param>
/// <param name="token">登录令牌；未登录时为 null。</param>
internal sealed class AdminSession(HttpClient http, string? token)
{
    /// <summary>
    /// 请求体的 JSON 设置：camelCase，和管理接口一致。
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 令牌；未登录时为 null。
    /// </summary>
    public string? Token { get; } = token;

    /// <summary>
    /// 登录。失败时返回响应和 null 会话。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="account">账号。</param>
    /// <param name="password">密码。</param>
    /// <param name="remember">是否“7 天内自动登录”。</param>
    /// <returns>登录的响应、会话（失败时为 null）和响应体。</returns>
    public static async Task<(HttpResponseMessage Response, AdminSession? Session, JsonElement Body)> LoginAsync(HttpClient http, string account, string password, bool remember = false)
    {
        var response = await http.PostAsJsonAsync("/api/admin/login", new
        {
            account,
            password,
            remember
        }, Json);
        var body = await ReadAsync(response);
        return response.IsSuccessStatusCode ? (response, new AdminSession(http, body.GetProperty("token").GetString()), body) : (response, null, body);
    }

    /// <summary>
    /// GET。
    /// </summary>
    /// <param name="path">接口路径，如 /api/admin/templates。</param>
    /// <returns>响应。</returns>
    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);

    /// <summary>
    /// POST JSON。
    /// </summary>
    /// <param name="path">接口路径。</param>
    /// <param name="body">请求体（转成 JSON）；没有时为 null。</param>
    /// <returns>响应。</returns>
    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    /// <summary>
    /// PUT JSON。
    /// </summary>
    /// <param name="path">接口路径。</param>
    /// <param name="body">请求体（转成 JSON）。</param>
    /// <returns>响应。</returns>
    public Task<HttpResponseMessage> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    /// <summary>
    /// DELETE。
    /// </summary>
    /// <param name="path">接口路径。</param>
    /// <returns>响应。</returns>
    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    /// <summary>
    /// 上传文件（multipart，字段名 file）。
    /// </summary>
    /// <param name="path">接口路径。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="content">文件内容。</param>
    /// <param name="contentType">文件的 Content-Type。</param>
    /// <returns>响应。</returns>
    public Task<HttpResponseMessage> UploadAsync(string path, string fileName, byte[] content, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return SendAsync(HttpMethod.Post, path, form);
    }

    /// <summary>
    /// 发请求并读出 JSON（没有内容时返回 undefined 的 JsonElement）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">接口路径。</param>
    /// <param name="body">请求体；没有时为 null。</param>
    /// <returns>响应体 JSON。</returns>
    public async Task<JsonElement> JsonAsync(HttpMethod method, string path, object? body = null) => await ReadAsync(await SendAsync(method, path, body));

    /// <summary>
    /// 读响应体 JSON；没有内容或不是 JSON 时返回 default。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>响应体 JSON（已复制，不依赖原文档）。</returns>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text) || response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) != true)
        {
            return default;
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>
    /// 错误响应里的 code。
    /// </summary>
    /// <param name="response">响应。</param>
    /// <returns>错误码，如 INVALID；不是错误响应时为 null。</returns>
    public static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var body = await ReadAsync(response);
        return body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>
    /// 发请求：带上令牌；请求体是 HttpContent 时原样发送，其他对象转成 JSON。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">接口路径。</param>
    /// <param name="body">请求体；没有时为 null。</param>
    /// <returns>响应。</returns>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        request.Content = body switch
        {
            null => null,
            HttpContent content => content,
            _ => JsonContent.Create(body, options: Json),
        };
        return await http.SendAsync(request);
    }
}
