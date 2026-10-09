using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace LabelService.Contracts;

/// <summary>
/// 模板 JSON 和开放接口共用的序列化设置。服务端、检查程序和基准测试都用这里的配置，保证同一份 JSON 读写结果一致。
/// </summary>
public static class LabelJson
{
    /// <summary>
    /// 只读的共享选项：camelCase 属性名、枚举写成字符串、不输出 null、允许 type 不在第一个属性。
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>
    /// 把统一设置写到外部传入的选项上，供 ASP.NET Core 的 JSON 配置复用。
    /// </summary>
    /// <param name="options">要修改的选项，必须还没有被使用过。</param>
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.AllowOutOfOrderMetadataProperties = true;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.AllowTrailingCommas = true;
        // 中文原样输出，方便人读；HTML 敏感字符仍然转义。
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        Apply(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
