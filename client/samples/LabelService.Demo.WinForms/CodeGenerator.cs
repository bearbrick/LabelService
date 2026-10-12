using System.Globalization;
using System.Text;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 按演示程序左边的当前设置生成可以直接复制的 C# 代码（“调用代码”页签）：建客户端、生成、发送到打印机（三种格式的打法都写全）、处理错误。
/// </summary>
/// <remarks>
/// 生成的代码只用 C# 7.3 的语法（.NET Framework 4.6.1+ 的默认版本），在新建的 .NET Framework 4.8 和 .NET 10 控制台项目里都能编译：
/// 引用 LabelService.Client 包后，把代码放进 Program.cs 即可。API Key 从环境变量读，不写进代码。
/// </remarks>
internal static partial class CodeGenerator
{
    /// <summary>
    /// 批量写法最多写出几条数据（再多只在注释里写总条数）。
    /// </summary>
    public const int MaxBatchRows = 5;

    /// <summary>
    /// 生成代码需要的设置。
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>
        /// 服务地址。
        /// </summary>
        public string BaseAddress { get; set; } = "http://localhost:5080/";

        /// <summary>
        /// 超时，秒。
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 自动重试次数。
        /// </summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// 模板编码。
        /// </summary>
        public string TemplateCode { get; set; } = "BOX_LABEL";

        /// <summary>
        /// 输出格式。
        /// </summary>
        public LabelFormat Format { get; set; } = LabelFormat.Pdf;

        /// <summary>
        /// 分辨率；null 表示用模板的，代码里不写。
        /// </summary>
        public int? Dpi
        {
            get; set;
        }

        /// <summary>
        /// 单张的份数；1 时代码里不写。
        /// </summary>
        public int Copies { get; set; } = 1;

        /// <summary>
        /// 字段 key、类型（string/number/date/image）和填的值，按表格顺序。
        /// </summary>
        public List<(string Key, string Type, string Value)> Fields { get; } = new List<(string Key, string Type, string Value)>();

        /// <summary>
        /// 生成批量写法（否则是单张写法）。
        /// </summary>
        public bool Batch
        {
            get; set;
        }

        /// <summary>
        /// 批量写法里写出的数据：每条的字段 key、类型和值（演示程序批量数据的前几行）；为空时用单张的值写一条。
        /// </summary>
        public List<List<(string Key, string Type, string Value)>> BatchRows { get; } = new List<List<(string Key, string Type, string Value)>>();

        /// <summary>
        /// 批量数据一共几条（只写出前几条时在注释里写明）。
        /// </summary>
        public int BatchTotal
        {
            get; set;
        }
    }

    /// <summary>
    /// 生成完整的 Program.cs。
    /// </summary>
    /// <param name="settings">演示程序左边的当前设置。</param>
    /// <returns>C# 源码（换行是 Environment.NewLine）。</returns>
    public static string Generate(Settings settings)
    {
        var format = "LabelFormat." + settings.Format;
        var code = new StringBuilder();
        AppendHeader(code);

        // System 开头的排前面，其余按字母顺序（和常见的 using 排法一致）
        var usings = new[] { "System", "System.IO", "System.Threading.Tasks", "LabelService.Client" }.Concat(PrintUsings)
            .OrderBy(name => name == "System" || name.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal);
        foreach (var name in usings)
        {
            code.AppendLine($"using {name};");
        }

        code.AppendLine();
        code.AppendLine("internal static class Program");
        code.AppendLine("{");
        AppendPrinterSettings(code);
        code.AppendLine("    private static async Task Main()");
        code.AppendLine("    {");
        // 生成的代码：建客户端（Key 从环境变量读）→ 单张或批量调用 → 发送到打印机 → 捕获 LabelServiceException 和打印机的错误。
        code.AppendLine("        // 应用内只建一个客户端实例，多线程共享。API Key 放配置文件或环境变量，不要写进代码。");
        code.AppendLine("        var client = new LabelServiceClient(new LabelServiceClientOptions");
        code.AppendLine("        {");
        code.AppendLine($"            BaseAddress = new Uri({Literal(settings.BaseAddress)}),");
        code.AppendLine("            ApiKey = Environment.GetEnvironmentVariable(\"LABEL_API_KEY\"),");
        code.AppendLine($"            Timeout = TimeSpan.FromSeconds({settings.TimeoutSeconds}),");
        code.AppendLine($"            MaxRetries = {settings.MaxRetries},");
        code.AppendLine("        });");
        code.AppendLine();
        code.AppendLine("        try");
        code.AppendLine("        {");
        if (settings.Batch)
        {
            AppendBatch(code, settings, format);
        }
        else
        {
            AppendSingle(code, settings, format);
        }

        AppendPrintCall(code);
        code.AppendLine("        }");
        code.AppendLine("        catch (LabelServiceException ex) when (ex.ErrorCode == LabelErrorCodes.FieldRequired)");
        code.AppendLine("        {");
        code.AppendLine("            foreach (var error in ex.Errors)");
        code.AppendLine("                Console.WriteLine(\"第 \" + ((error.Index ?? 0) + 1) + \" 条缺少字段 \" + error.Field);");
        code.AppendLine("        }");
        code.AppendLine("        catch (LabelServiceException ex)");
        code.AppendLine("        {");
        code.AppendLine("            // ex.IsTransient 为 true 时 SDK 已经自动重试过，可以稍后再试；反馈问题时带上 traceId。");
        code.AppendLine("            Console.WriteLine(ex.ErrorCode + \"：\" + ex.Message + \"（traceId \" + ex.TraceId + \"）\");");
        code.AppendLine("        }");
        AppendPrintCatch(code);
        code.AppendLine("    }");
        AppendPrintMethods(code);
        code.AppendLine("}");
        return code.ToString();
    }

    /// <summary>
    /// 写单张调用：RenderRequest 加字段值、DPI、份数（ZPL 没指定 DPI、PDF 和 PNG 的拼版写成注释），调 RenderAsync。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    /// <param name="format">LabelFormat 枚举的写法，如 LabelFormat.Pdf。</param>
    private static void AppendSingle(StringBuilder code, Settings settings, string format)
    {
        code.AppendLine($"            var request = new RenderRequest({Literal(settings.TemplateCode)}, {format})");
        foreach (var (key, type, value) in settings.Fields.Where(field => field.Value.Length > 0))
        {
            code.AppendLine($"                .Set({Literal(key)}, {Value(type, value)})");
        }

        code.Length -= Environment.NewLine.Length;
        code.AppendLine(";");
        if (settings.Dpi is { } dpi)
        {
            code.AppendLine($"            request.Dpi = {dpi};");
        }

        if (settings.Copies != 1)
        {
            code.AppendLine($"            request.Copies = {settings.Copies};");
        }

        AppendOptionHints(code, settings, "request");

        code.AppendLine();
        code.AppendLine("            LabelFile file = await client.RenderAsync(request);");
        code.AppendLine("            Console.WriteLine(file.FileName + \"，\" + file.LabelCount + \" 张，服务端 \" + file.ServerDuration?.TotalMilliseconds + \" ms，traceId \" + file.TraceId);");
    }

    /// <summary>
    /// 写批量调用：每条用 AddItem().Set(...) 把字段给全（调用方准备好每条的数据，不用公共字段），PNG 时选 ZIP 输出；ZPL 没指定 DPI、PDF 和 PNG 的拼版写成注释。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    /// <param name="format">LabelFormat 枚举的写法，如 LabelFormat.Pdf。</param>
    private static void AppendBatch(StringBuilder code, Settings settings, string format)
    {
        code.Append($"            var batch = new BatchRenderRequest({Literal(settings.TemplateCode)}, {format})");
        code.AppendLine(settings.Format == LabelFormat.Png ? " { Output = BatchOutput.Zip };   // PNG 一个文件只能放一张，批量要打包成 ZIP" : ";");
        if (settings.Dpi is { } dpi)
        {
            code.AppendLine($"            batch.Dpi = {dpi};");
        }

        AppendOptionHints(code, settings, "batch");

        code.AppendLine();
        code.AppendLine("            // 每条标签的数据都由调用方给全，要几张就加几条，SDK 原样发给服务端。");
        code.AppendLine("            // 实际使用时循环业务系统的数据（装箱明细、订单行……）逐条添加；下面是演示程序批量数据里的数据。");
        var rows = settings.BatchRows.Count > 0
            ? settings.BatchRows
            : new List<List<(string Key, string Type, string Value)>> { settings.Fields.Where(field => field.Value.Length > 0).ToList() };
        foreach (var row in rows)
        {
            code.Append("            batch.AddItem()");
            foreach (var (key, type, value) in row)
            {
                code.AppendLine();
                code.Append($"                .Set({Literal(key)}, {Value(type, value)})");
            }

            code.AppendLine(";");
        }

        if (settings.BatchTotal > rows.Count)
        {
            code.AppendLine($"            // ……演示程序的批量数据一共 {settings.BatchTotal} 条，这里只写出前 {rows.Count} 条");
        }

        code.AppendLine();
        code.AppendLine("            LabelFile file = await client.RenderBatchAsync(batch);");
        code.AppendLine("            Console.WriteLine(file.FileName + \"，\" + file.LabelCount + \" 张，服务端 \" + file.ServerDuration?.TotalMilliseconds + \" ms\");");
    }

    /// <summary>
    /// 写成注释的可选设置：ZPL 没指定 DPI 时提示要和打印机一致；PDF、PNG 提示可以拼版（多张排在 A4 纸上，打出来是多页）。
    /// </summary>
    /// <param name="code">正在生成的代码。</param>
    /// <param name="settings">当前设置。</param>
    /// <param name="variable">请求变量名：request 或 batch。</param>
    private static void AppendOptionHints(StringBuilder code, Settings settings, string variable)
    {
        if (settings.Format == LabelFormat.Zpl)
        {
            if (settings.Dpi is null)
            {
                code.AppendLine($"            // {variable}.Dpi = 300;   // ZPL 要和标签打印机的分辨率一致（203、300 或 600），不写时用模板里设的");
            }

            return;
        }

        code.AppendLine($"            // {variable}.Layout = \"A4_100x60_2x4\";   // 拼版：多张排在 A4 等纸上打，打出来是多页；版式编码在管理后台“拼版版式”里看（ZPL 不支持）");
    }

    /// <summary>
    /// 字段值在生成代码里的写法：数字写成字面量（带小数点的加 m），日期写成 new DateTime，图片写成 File.ReadAllBytes。
    /// </summary>
    /// <param name="type">字段类型。</param>
    /// <param name="value">表格里的文本。</param>
    /// <returns>C# 表达式。</returns>
    private static string Value(string type, string value)
    {
        if (type == "number" && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture) + (value.Contains('.') ? "m" : "");
        }

        if (type == "date" && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return $"new DateTime({date.Year}, {date.Month}, {date.Day})";
        }

        if (type == "image")
        {
            return $"File.ReadAllBytes({Literal(value)})";
        }

        return Literal(value);
    }

    /// <summary>
    /// C# 字符串字面量（转义反斜杠、引号和换行）。
    /// </summary>
    /// <param name="value">文本。</param>
    /// <returns>带双引号的字面量。</returns>
    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
}
