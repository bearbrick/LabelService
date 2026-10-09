using System.Text;
using System.Text.RegularExpressions;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using ZXing;
using ZXing.Common;

namespace LabelService.Rendering.Elements;

/// <summary>
/// 一维码编码：检查内容是否符合码制、补校验位，再用 ZXing.Net 编码出模块序列（不带空白区）。绘制由 <see cref="BarcodePainter"/> 按打印点完成。
/// </summary>
internal static partial class LinearSymbols
{
    /// <summary>
    /// GS1-128 的 FNC1（ZXing 的 Code128 编码器用这个字符表示 FNC1）。
    /// </summary>
    private const char Fnc1 = 'ñ';

    /// <summary>
    /// 数据长度固定的 GS1 应用标识符（按前两位），值是“标识符 + 数据”的总长度。不在表里的是变长，后面要加 FNC1 分隔。
    /// </summary>
    private static readonly Dictionary<string, int> FixedLengthAis = new(StringComparer.Ordinal)
    {
        ["00"] = 20,
        ["01"] = 16,
        ["02"] = 16,
        ["03"] = 16,
        ["04"] = 18,
        ["11"] = 8,
        ["12"] = 8,
        ["13"] = 8,
        ["14"] = 8,
        ["15"] = 8,
        ["16"] = 8,
        ["17"] = 8,
        ["18"] = 8,
        ["19"] = 8,
        ["20"] = 4,
        ["31"] = 10,
        ["32"] = 10,
        ["33"] = 10,
        ["34"] = 10,
        ["35"] = 10,
        ["36"] = 10,
        ["41"] = 16,
    };

    /// <summary>
    /// 编码（带缓存）。内容不符合码制时抛 BARCODE_INVALID。
    /// </summary>
    public static EncodedSymbol Encode(BarcodeSymbology symbology, string content, string elementId) =>
        SymbolCache.GetOrAdd("1d:" + symbology, content, value => EncodeUncached(symbology, value, elementId));

    private static EncodedSymbol EncodeUncached(BarcodeSymbology symbology, string content, string elementId)
    {
        LabelRenderException Invalid(string message) =>
            new(ErrorCodes.BarcodeInvalid, $"元素 {elementId} 的条码内容“{content}”不符合 {Name(symbology)}：{message}", elementId);

        if (content.Length == 0)
        {
            throw Invalid("内容为空");
        }

        var (format, data, caption) = symbology switch
        {
            BarcodeSymbology.Code128 => (BarcodeFormat.CODE_128, Ascii(content, Invalid), content),
            BarcodeSymbology.Code39 => (BarcodeFormat.CODE_39, Ascii(content, Invalid), content),
            BarcodeSymbology.Ean13 => Digits(BarcodeFormat.EAN_13, content, 13, Invalid),
            BarcodeSymbology.Ean8 => Digits(BarcodeFormat.EAN_8, content, 8, Invalid),
            BarcodeSymbology.Upca => Digits(BarcodeFormat.UPC_A, content, 12, Invalid),
            BarcodeSymbology.Itf14 => Digits(BarcodeFormat.ITF, content, 14, Invalid),
            BarcodeSymbology.Gs1128 => (BarcodeFormat.CODE_128, Gs1(content, Invalid), content),
            _ => throw Invalid("不支持的码制"),
        };

        bool[] modules;
        try
        {
            var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
            var matrix = new MultiFormatWriter().encode(data, format, 1, 1, hints);
            modules = new bool[matrix.Width];
            for (var x = 0; x < modules.Length; x++)
            {
                modules[x] = matrix[x, 0];
            }
        }
        catch (ArgumentException ex)
        {
            throw Invalid(ex.Message);
        }
        catch (WriterException ex)
        {
            throw Invalid(ex.Message);
        }

        return new EncodedSymbol(modules.Length, 1, modules, caption);
    }

    /// <summary>
    /// 码制的显示名，用在错误消息里。
    /// </summary>
    public static string Name(BarcodeSymbology symbology) => symbology switch
    {
        BarcodeSymbology.Code128 => "Code128",
        BarcodeSymbology.Code39 => "Code39",
        BarcodeSymbology.Ean13 => "EAN-13",
        BarcodeSymbology.Ean8 => "EAN-8",
        BarcodeSymbology.Upca => "UPC-A",
        BarcodeSymbology.Itf14 => "ITF-14",
        BarcodeSymbology.Gs1128 => "GS1-128",
        _ => symbology.ToString(),
    };

    /// <summary>
    /// 按 GS1 规则算校验位（EAN、UPC、ITF-14 通用）：从右往左奇数位乘 3。
    /// </summary>
    public static int CheckDigit(ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }

        return (10 - sum % 10) % 10;
    }

    private static string Ascii(string content, Func<string, LabelRenderException> invalid) =>
        content.All(ch => ch < 128) ? content : throw invalid("只能编码英文字母、数字和符号，不能有中文或全角字符");

    /// <summary>
    /// 纯数字码制：少一位时补校验位，位数够时核对校验位。
    /// </summary>
    private static (BarcodeFormat Format, string Data, string Caption) Digits(
        BarcodeFormat format, string content, int length, Func<string, LabelRenderException> invalid)
    {
        if (!content.All(char.IsAsciiDigit))
        {
            throw invalid("只能是数字");
        }

        if (content.Length == length - 1)
        {
            var full = content + CheckDigit(content);
            return (format, full, full);
        }

        if (content.Length != length)
        {
            throw invalid($"应为 {length - 1} 位（自动补校验位）或 {length} 位数字");
        }

        if (CheckDigit(content.AsSpan(0, length - 1)) != content[^1] - '0')
        {
            throw invalid($"校验位应为 {CheckDigit(content.AsSpan(0, length - 1))}");
        }

        return (format, content, content);
    }

    /// <summary>
    /// GS1-128：内容写成“(AI)数据(AI)数据”，开头加 FNC1，变长数据后面（不是最后一段时）加 FNC1 分隔。
    /// </summary>
    private static string Gs1(string content, Func<string, LabelRenderException> invalid)
    {
        var matches = Gs1Segment().Matches(content);
        if (matches.Count == 0 || string.Concat(matches.Select(match => match.Value)) != content)
        {
            throw invalid("要写成 (应用标识符)数据 的形式，如 (01)06901234567892(10)ABC");
        }

        var builder = new StringBuilder().Append(Fnc1);
        for (var i = 0; i < matches.Count; i++)
        {
            var ai = matches[i].Groups["ai"].Value;
            var data = matches[i].Groups["data"].Value;
            if (data.Length == 0 || !data.All(ch => ch is >= '!' and <= 'z'))
            {
                throw invalid($"({ai}) 后面的数据为空或含有不允许的字符");
            }

            builder.Append(ai).Append(data);
            if (FixedLengthAis.TryGetValue(ai[..2], out var total))
            {
                if (ai.Length + data.Length != total)
                {
                    throw invalid($"({ai}) 后面应为 {total - ai.Length} 位");
                }
            }
            else if (i < matches.Count - 1)
            {
                builder.Append(Fnc1);
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\((?<ai>\d{2,4})\)(?<data>[^()]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Gs1Segment();
}
