using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace LabelService.Rendering.Assets;

/// <summary>
/// SVG 清洗：交给 Svg.Skia 之前去掉一切会执行或会访问外部的内容（设计书第 9 节“安全”）。
/// </summary>
/// <remarks>
/// <para>实测 Svg.Skia 会按 &lt;image href&gt; 去请求外部地址，所以图标、上传的 SVG 素材一律先清洗：</para>
/// <list type="bullet">
/// <item>禁止 DTD（防 XXE、实体膨胀），不解析外部资源。</item>
/// <item>删除 script、foreignObject、iframe、object、embed、audio、video、style 和动画元素。</item>
/// <item>删除 on* 事件属性；href、xlink:href 只保留文档内引用（#id）和内嵌图片（data:image/...）。</item>
/// <item>属性值里的 url(...) 只保留 url(#id)。</item>
/// </list>
/// </remarks>
public static partial class SvgSanitizer
{
    private static readonly HashSet<string> RemovedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "iframe", "object", "embed", "audio", "video", "style",
        "animate", "animateMotion", "animateTransform", "set", "discard", "handler", "listener",
    };

    /// <summary>
    /// 清洗 SVG 文本。不是合法的 SVG 时抛 <see cref="XmlException"/> 或 <see cref="InvalidDataException"/>。
    /// </summary>
    public static string Sanitize(string svg)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
        using var reader = XmlReader.Create(new StringReader(svg), settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        if (document.Root is not { } root || root.Name.LocalName != "svg")
        {
            throw new InvalidDataException("根元素不是 svg");
        }

        foreach (var element in root.DescendantsAndSelf().ToList())
        {
            if (RemovedElements.Contains(element.Name.LocalName))
            {
                element.Remove();
                continue;
            }

            foreach (var attribute in element.Attributes().ToList())
            {
                if (!IsSafe(attribute))
                {
                    attribute.Remove();
                }
            }
        }

        foreach (var node in root.DescendantNodes().OfType<XProcessingInstruction>().ToList())
        {
            node.Remove();
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static bool IsSafe(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            return true;
        }

        var name = attribute.Name.LocalName;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = attribute.Value.Trim();
        if (name.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            return value.StartsWith('#') || value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
        }

        // url(...) 只允许引用文档内的元素，如渐变 fill="url(#g)"。
        return UrlReference().Matches(value).All(match => match.Groups["target"].Value.TrimStart('\'', '"', ' ').StartsWith('#'));
    }

    [GeneratedRegex(@"url\s*\((?<target>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlReference();
}
