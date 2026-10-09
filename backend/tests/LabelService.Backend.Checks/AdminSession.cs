using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LabelService.Backend.Checks;

/// <summary>
/// 检查程序里调管理接口用的会话：登录拿令牌，之后每个请求带 Authorization: Bearer。
/// </summary>
internal sealed class AdminSession(HttpClient http, string? token)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>令牌；未登录时为 null。</summary>
    public string? Token { get; } = token;

    /// <summary>
    /// 登录。失败时返回响应和 null 会话。
    /// </summary>
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

    /// <summary>GET。</summary>
    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);

    /// <summary>POST JSON。</summary>
    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    /// <summary>PUT JSON。</summary>
    public Task<HttpResponseMessage> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    /// <summary>DELETE。</summary>
    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    /// <summary>上传文件（multipart，字段名 file）。</summary>
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
    public async Task<JsonElement> JsonAsync(HttpMethod method, string path, object? body = null) => await ReadAsync(await SendAsync(method, path, body));

    /// <summary>
    /// 读响应体 JSON；没有内容或不是 JSON 时返回 default。
    /// </summary>
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
    public static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var body = await ReadAsync(response);
        return body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

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
