using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LabelService.Contracts;
using LabelService.Contracts.Layouts;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Binding;
using LabelService.Rendering.Elements;

namespace LabelService.Backend.Checks;

/// <summary>
/// 契约检查：示例模板、JSON 写法，以及错误码、HTTP 头、元素类型在后端、调用端（client/）、前端（frontend/）、文档之间保持一致。
/// </summary>
/// <remarks>
/// 三个部分互不引用项目，所以这里按文本读取 client/ 和 frontend/ 的源码做比对。
/// </remarks>
internal static partial class ContractChecks
{
    /// <summary>
    /// 运行契约一致性的全部检查。
    /// </summary>
    /// <param name="backendRoot">backend 目录。</param>
    /// <param name="repositoryRoot">仓库根目录。</param>
    /// <param name="check">记录检查结果。</param>
    public static void Run(string backendRoot, string repositoryRoot, Action<string, bool> check)
    {
        var templates = Directory.GetFiles(Path.Combine(backendRoot, "samples", "templates"), "*.json");
        check("示例模板存在", templates.Length > 0);
        foreach (var path in templates)
        {
            var name = Path.GetFileName(path);
            var template = JsonSerializer.Deserialize<LabelTemplate>(File.ReadAllText(path), LabelJson.Options)!;
            var keys = template.Fields.Select(field => field.Key).ToHashSet(StringComparer.Ordinal);
            check($"{name}：字段 key 唯一", keys.Count == template.Fields.Count);
            check($"{name}：元素 ID 唯一", template.Elements.Select(element => element.Id).Distinct().Count() == template.Elements.Count);
            check($"{name}：绑定只引用已定义的字段", template.Elements.SelectMany(BoundTexts).SelectMany(BindingResolver.FindKeys).All(keys.Contains));
            var once = JsonSerializer.Serialize(template, LabelJson.Options);
            var twice = JsonSerializer.Serialize(JsonSerializer.Deserialize<LabelTemplate>(once, LabelJson.Options), LabelJson.Options);
            check($"{name}：序列化往返不丢内容", once == twice);
        }

        var barcode = JsonSerializer.Serialize<LabelElement>(new BarcodeElement { Symbology = BarcodeSymbology.Gs1128 }, LabelJson.Options);
        check("元素 type 和码制按规范写：barcode、gs1-128", barcode.Contains("\"type\":\"barcode\"") && barcode.Contains("\"symbology\":\"gs1-128\""));
        var qr = JsonSerializer.Serialize<LabelElement>(new QrCodeElement { Symbology = TwoDimensionalSymbology.DataMatrix, EcLevel = ErrorCorrectionLevel.H }, LabelJson.Options);
        check("二维码按规范写：datamatrix、纠错级别大写", qr.Contains("\"symbology\":\"datamatrix\"") && qr.Contains("\"ecLevel\":\"H\""));
        var outOfOrder = JsonSerializer.Deserialize<LabelElement>("""{"id":"x","x":1,"type":"rect","fill":"#000000"}""", LabelJson.Options);
        check("type 不在第一个属性也能解析", outOfOrder is RectElement { Fill: "#000000" });
        check("未知元素类型解析失败", Throws(() => JsonSerializer.Deserialize<LabelTemplate>("""{"elements":[{"type":"table"}]}""", LabelJson.Options)));

        var contractCodes = Constants(typeof(ErrorCodes));
        var contractHeaders = Constants(typeof(HeaderNames));
        check("每个错误码都有 HTTP 状态码", contractCodes.SetEquals(ErrorCodes.StatusCodes.Keys));
        check("Server-Timing 阶段以 total 结尾", TimingStages.All[^1] == TimingStages.Total);

        var clientSource = Path.Combine(repositoryRoot, "client", "src", "LabelService.Client");
        var clientCodes = SourceConstants(Path.Combine(clientSource, "LabelErrorCodes.cs"));
        check("调用端 SDK 的 LabelErrorCodes 覆盖全部服务端错误码", contractCodes.IsSubsetOf(clientCodes));
        check("调用端 SDK 独有的错误码只有客户端错误",
            clientCodes.Except(contractCodes).Order().SequenceEqual(["NETWORK_ERROR", "TIMEOUT", "UNEXPECTED_RESPONSE"]));
        check("调用端 SDK 的 HTTP 头和服务端一致", contractHeaders.SetEquals(SourceConstants(Path.Combine(clientSource, "Internal", "Protocol.cs"))));

        var contractsDoc = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "contracts.md"));
        check("docs/contracts.md 写到了全部错误码", clientCodes.All(contractsDoc.Contains));
        check("docs/contracts.md 写到了全部元素类型", ElementTypes.All.All(type => contractsDoc.Contains($"`{type}`")));
        var progress = ElementTypes.All.Where(type => Regex.IsMatch(contractsDoc, $@"^\| `{type}` \|.*\| 已实现 \|\r?$", RegexOptions.Multiline)).Order().ToArray();
        check("docs/contracts.md 元素表的“已实现”和服务端已登记的绘制器一致：" + string.Join("、", progress),
            progress.SequenceEqual(ElementPainters.Implemented.Order()));
        check("docs/contracts.md 写到了全部 HTTP 头", contractHeaders.All(contractsDoc.Contains));

        var frontendModel = File.ReadAllText(Path.Combine(repositoryRoot, "frontend", "src", "model", "template.ts"));
        check("前端 TypeScript 模型包含全部元素类型", ElementTypes.All.All(type => frontendModel.Contains($"'{type}'")));
        var frontendSample = JsonNode.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "frontend", "src", "samples", "box-label.json")));
        var backendSample = JsonNode.Parse(File.ReadAllText(Path.Combine(backendRoot, "samples", "templates", "box-label.json")));
        check("前端的示例模板和后端一致", JsonNode.DeepEquals(frontendSample, backendSample));

        // 拼版版式：前端 frontend/src/model/layout.ts 是 SheetLayout 的镜像，按 JSON 属性名核对。
        var frontendLayout = File.ReadAllText(Path.Combine(repositoryRoot, "frontend", "src", "model", "layout.ts"));
        var layoutJson = JsonSerializer.SerializeToNode(new SheetLayout { Slots = [new SheetSlot()] }, LabelJson.Options)!.AsObject();
        var layoutKeys = layoutJson.Select(pair => pair.Key)
            .Concat(layoutJson.SelectMany(pair => pair.Value switch
            {
                JsonObject child => child.Select(inner => inner.Key),
                JsonArray { Count: > 0 } array when array[0] is JsonObject item => item.Select(inner => inner.Key),
                _ => [],
            }))
            .Distinct()
            .ToArray();
        var missingLayoutKeys = layoutKeys.Where(key => !Regex.IsMatch(frontendLayout, $@"^\s+{key}: ", RegexOptions.Multiline)).ToArray();
        check("前端的版式模型（frontend/src/model/layout.ts）和后端 SheetLayout 的属性一致" + (missingLayoutKeys.Length > 0 ? "：缺 " + string.Join(", ", missingLayoutKeys) : ""),
            layoutKeys.Length >= 10 && missingLayoutKeys.Length == 0);

        // 图标：设计器和服务端各一份（三个部分互不引用），按文本核对。图片素材设计器从服务端素材库读取，不再各存一份。
        var frontendIcons = FrontendIcon().Matches(File.ReadAllText(Path.Combine(repositoryRoot, "frontend", "src", "model", "icons.ts")))
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["d"].Value);
        var iconRoot = Path.Combine(backendRoot, "assets", "icons");
        var backendIcons = Directory.GetFiles(iconRoot, "*.svg", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.ChangeExtension(Path.GetRelativePath(iconRoot, path), null).Replace('\\', '/'),
                path => SvgPath().Match(File.ReadAllText(path)).Groups["d"].Value);
        var iconMismatch = frontendIcons.Keys.Union(backendIcons.Keys)
            .Where(key => frontendIcons.GetValueOrDefault(key) != backendIcons.GetValueOrDefault(key))
            .Order()
            .ToArray();
        check("设计器和服务端的图标一致（frontend/src/model/icons.ts 对 backend/assets/icons）" + (iconMismatch.Length > 0 ? "：" + string.Join(", ", iconMismatch) : ""),
            frontendIcons.Count > 0 && iconMismatch.Length == 0);
        var frontendAssets = File.ReadAllText(Path.Combine(repositoryRoot, "frontend", "src", "stores", "assets.ts"));
        check("设计器的图片素材从服务端素材库读取（frontend/src/stores/assets.ts 不自带素材）",
            frontendAssets.Contains("listAssets") && !frontendAssets.Contains("data:image"));
    }

    /// <summary>
    /// 前端 icons.ts 里一个图标的定义：key 是 gb191/xxx，d 是 SVG 路径。
    /// </summary>
    /// <returns>编译好的正则，key 和 d 两个组。</returns>
    [GeneratedRegex(@"'(?<key>gb191/[^']+)': \{ name: '[^']+', d: '(?<d>[^']+)' \}")]
    private static partial Regex FrontendIcon();

    /// <summary>
    /// SVG 文件里 path 元素的 d 属性。
    /// </summary>
    /// <returns>编译好的正则，路径在 d 组。</returns>
    [GeneratedRegex(@"<path d=""(?<d>[^""]+)""")]
    private static partial Regex SvgPath();

    /// <summary>
    /// 元素里可以写绑定的文字（文本内容、条码和二维码的值、图片来源）。
    /// </summary>
    /// <param name="element">元素。</param>
    /// <returns>可以带绑定的文字；其他元素为空。</returns>
    private static IEnumerable<string> BoundTexts(LabelElement element) => element switch
    {
        TextElement text => [text.Content],
        BarcodeElement code => [code.Value],
        QrCodeElement code => [code.Value],
        ImageElement image => [image.Src],
        _ => [],
    };

    /// <summary>
    /// 用反射取一个类型里全部字符串常量的值。
    /// </summary>
    /// <param name="type">常量类，如 ErrorCodes。</param>
    /// <returns>常量值的集合。</returns>
    private static HashSet<string> Constants(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 按文本取一个 C# 源文件里全部字符串常量的值（调用端副本不能被引用，只能读文本）。
    /// </summary>
    /// <param name="path">源文件路径。</param>
    /// <returns>常量值的集合。</returns>
    private static HashSet<string> SourceConstants(string path) => StringConstant()
        .Matches(File.ReadAllText(path))
        .Select(match => match.Groups["value"].Value)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 执行一个操作，看它是否抛出任何异常。
    /// </summary>
    /// <param name="action">操作。</param>
    /// <returns>抛出异常时为 true。</returns>
    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// C# 源码里的字符串常量声明（const string 名称 = "值";）。
    /// </summary>
    /// <returns>编译好的正则，值在 value 组。</returns>
    [GeneratedRegex("""const\s+string\s+\w+\s*=\s*"(?<value>[^"]*)"\s*;""")]
    private static partial Regex StringConstant();
}
