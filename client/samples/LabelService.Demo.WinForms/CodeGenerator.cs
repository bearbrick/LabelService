using System.Globalization;
using System.Text;
using LabelService.Client;

namespace LabelService.Demo;

/// <summary>
/// 按演示程序左边的当前设置生成可以直接复制的 C# 代码（“调用代码”页签）。
/// </summary>
/// <remarks>
/// 生成的代码只用 C# 7.3 的语法（.NET Framework 4.6.2+ 的默认版本），在新建的 .NET Framework 4.8 和 .NET 10 控制台项目里都能编译：
/// 引用 LabelService.Client 包后，把代码放进 Program.cs 即可。API Key 从环境变量读，不写进代码。
/// </remarks>
internal static class CodeGenerator
{
    /// <summary>生成代码需要的设置。</summary>
    internal sealed class Settings
    {
        public string BaseAddress { get; set; } = "http://localhost:5080/";

        public int TimeoutSeconds { get; set; } = 30;

        public int MaxRetries { get; set; } = 2;

        public string TemplateCode { get; set; } = "BOX_LABEL";

        public LabelFormat Format { get; set; } = LabelFormat.Pdf;

        public int? Dpi
        {
            get; set;
        }

        public int Copies { get; set; } = 1;

        /// <summary>字段 key、类型（string/number/date/image）和填的值，按表格顺序。</summary>
        public List<(string Key, string Type, string Value)> Fields { get; } = new List<(string Key, string Type, string Value)>();

        public bool Batch
        {
            get; set;
        }

        public int BatchCount { get; set; } = 10;

        public string? IncrementKey
        {
            get; set;
        }
    }

    /// <summary>生成完整的 Program.cs。</summary>
    public static string Generate(Settings settings)
    {
        var format = "LabelFormat." + settings.Format;
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.IO;");
        code.AppendLine("using System.Threading.Tasks;");
        code.AppendLine("using LabelService.Client;");
        code.AppendLine();
        code.AppendLine("internal static class Program");
        code.AppendLine("{");
        code.AppendLine("    private static async Task Main()");
        code.AppendLine("    {");
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
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }

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

        code.AppendLine();
        code.AppendLine("            LabelFile file = await client.RenderAsync(request);");
        code.AppendLine("            file.Save(Path.Combine(Path.GetTempPath(), file.FileName));");
        code.AppendLine("            Console.WriteLine(file.FileName + \"，\" + file.LabelCount + \" 张，服务端 \" + file.ServerDuration?.TotalMilliseconds + \" ms，traceId \" + file.TraceId);");
    }

    private static void AppendBatch(StringBuilder code, Settings settings, string format)
    {
        var increment = settings.Fields.FirstOrDefault(field => field.Key == settings.IncrementKey);
        code.AppendLine($"            var batch = new BatchRenderRequest({Literal(settings.TemplateCode)}, {format})");
        if (settings.Format == LabelFormat.Png)
        {
            code.AppendLine("            {");
            code.AppendLine("                Output = BatchOutput.Zip,   // PNG 一个文件只能放一张，批量要打包成 ZIP");
            code.AppendLine("            }");
        }

        foreach (var (key, type, value) in settings.Fields.Where(field => field.Value.Length > 0 && field.Key != settings.IncrementKey))
        {
            code.AppendLine($"                .SetCommon({Literal(key)}, {Value(type, value)})");
        }

        code.Length -= Environment.NewLine.Length;
        code.AppendLine(";");
        if (settings.Dpi is { } dpi)
        {
            code.AppendLine($"            batch.Dpi = {dpi};");
        }

        code.AppendLine();
        code.AppendLine($"            for (var i = 0; i < {settings.BatchCount}; i++)");
        code.AppendLine("            {");
        if (increment.Key is null)
        {
            code.AppendLine("                batch.AddItem();");
        }
        else
        {
            code.AppendLine($"                // 每条的 {increment.Key} 递增：{increment.Value}、{Increment(increment.Value, 1)}、{Increment(increment.Value, 2)}……");
            code.AppendLine($"                batch.AddItem().Set({Literal(increment.Key)}, {IncrementExpression(increment.Value)});");
        }

        code.AppendLine("            }");
        code.AppendLine();
        code.AppendLine("            LabelFile file = await client.RenderBatchAsync(batch);");
        code.AppendLine("            file.Save(Path.Combine(Path.GetTempPath(), file.FileName));");
        code.AppendLine("            Console.WriteLine(file.FileName + \"，\" + file.LabelCount + \" 张，服务端 \" + file.ServerDuration?.TotalMilliseconds + \" ms\");");
    }

    /// <summary>
    /// 把末尾的数字加 <paramref name="step"/>，保持位数：C2610080001 → C2610080002；没有数字时在后面加序号。
    /// </summary>
    public static string Increment(string value, long step)
    {
        var digits = value.Length - Enumerable.Reverse(value).TakeWhile(char.IsDigit).Count();
        if (digits == value.Length || value.Length - digits > 18)
        {
            return step == 0 ? value : value + "-" + step.ToString(CultureInfo.InvariantCulture);
        }

        var number = long.Parse(value.Substring(digits), CultureInfo.InvariantCulture) + step;
        return value.Substring(0, digits) + number.ToString(CultureInfo.InvariantCulture).PadLeft(value.Length - digits, '0');
    }

    private static string IncrementExpression(string value)
    {
        var digits = value.Length - Enumerable.Reverse(value).TakeWhile(char.IsDigit).Count();
        if (digits == value.Length || value.Length - digits > 18)
        {
            return $"{Literal(value + "-")} + i";
        }

        var start = long.Parse(value.Substring(digits), CultureInfo.InvariantCulture);
        return $"{Literal(value.Substring(0, digits))} + ({start}L + i).ToString(\"D{value.Length - digits}\")";
    }

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

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
}
