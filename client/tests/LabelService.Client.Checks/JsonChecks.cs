using System.Text;
using LabelService.Client.Internal;

namespace LabelService.Client.Checks;

/// <summary>
/// SDK 自带的 JSON 读写（不依赖 System.Text.Json）和响应映射：转义、中文、数字、嵌套、格式错误、属性名大小写。
/// </summary>
internal static class JsonChecks
{
    /// <summary>
    /// 运行 JSON 读写和响应映射的全部检查。
    /// </summary>
    /// <param name="check">记录检查结果。</param>
    public static void Run(Action<string, bool> check)
    {
        var body = new Dictionary<string, object?>
        {
            ["text"] = "引号\"反斜杠\\换行\n制表\t控制\u0001",
            ["int"] = 50,
            ["decimal"] = 1.50m,
            ["double"] = 0.1,
            ["long"] = 12345678901234L,
            ["flag"] = false,
            ["none"] = null,
            ["list"] = new List<Dictionary<string, object?>> { new Dictionary<string, object?> { ["a"] = 1 } },
        };
        var written = Encoding.UTF8.GetString(JsonText.Serialize(body));
        check("JSON 写：转义引号、反斜杠、换行、控制字符，中文原样，属性按添加顺序",
            written == "{\"text\":\"引号\\\"反斜杠\\\\换行\\n制表\\t控制\\u0001\",\"int\":50,\"decimal\":1.50,\"double\":0.1,\"long\":12345678901234,\"flag\":false,\"none\":null,\"list\":[{\"a\":1}]}");
        check("JSON 写：NaN、无穷大报参数错误", Throws<ArgumentException>(() => JsonText.Serialize(new Dictionary<string, object?> { ["x"] = double.NaN })));

        var parsed = JsonText.Parse("﻿ { \"s\" : \"\\u4e2d\\ud83d\\ude00\\/\" , \"n\": -1.5e2, \"i\": 7, \"b\": true, \"z\": null, \"o\": {\"k\": [1, \"two\", []]} } ") as Dictionary<string, object?>;
        check("JSON 读：BOM、空白、\\u 转义（含代理对）、数字读成 decimal、嵌套对象和数组",
            parsed != null && (string?)parsed["s"] == "中😀/" && parsed["n"] is decimal n && n == -150m && parsed["i"] is decimal i && i == 7m
            && parsed["b"] is true && parsed["z"] == null && parsed["o"] is Dictionary<string, object?> o && o["k"] is List<object?> { Count: 3 } k && k[2] is List<object?> { Count: 0 });
        var roundTrip = JsonText.Parse(written) as Dictionary<string, object?>;
        check("JSON 读写往返一致", roundTrip != null && (string?)roundTrip["text"] == (string)body["text"]! && roundTrip["decimal"] is decimal d && d == 1.50m);

        string[] malformed = ["{\"a\":1,}", "{\"a\":\"no end}", "[1 2]", "{'a':1}", "{\"a\":01x}", "tru", "{\"a\":1} x", new string('[', 100) + new string(']', 100), "\"tab\there\""];
        check("JSON 读：格式不对（多余逗号、没结束、单引号、多余内容、嵌套超过 64 层、没转义的控制字符）都报 FormatException",
            malformed.All(text => Throws<FormatException>(() => JsonText.Parse(text))));

        var schema = Responses.ToSchema(JsonText.Parse(
            "{\"TemplateCode\":\"BOX\",\"name\":\"成品箱标\",\"version\":3,\"page\":{\"width\":100,\"height\":60.5,\"dpi\":300},"
            + "\"fields\":[{\"key\":\"qty\",\"type\":\"number\",\"default\":1},{\"key\":\"day\",\"type\":\"date\",\"required\":false,\"auto\":\"today\",\"maxLength\":10}],"
            + "\"sample\":{\"qty\":50,\"name\":\"电源\",\"ok\":true},\"extra\":{\"ignored\":1}}"));
        check("字段定义映射：属性名不分大小写，required 默认 true，default、auto、maxLength，样例值是 decimal、string、bool",
            schema is { TemplateCode: "BOX", Name: "成品箱标", Version: 3 } && schema.Page.Width == 100 && schema.Page.Height == 60.5 && schema.Page.Dpi == 300
            && schema.Fields[0] is { Key: "qty", Type: "number", Required: true, Default: 1m } && schema.Fields[1] is { Required: false, Auto: "today", MaxLength: 10 }
            && schema.Sample["qty"] is 50m && schema.Sample["name"] as string == "电源" && schema.Sample["ok"] is true);
        check("字段定义映射：样例值可以直接传给 RenderRequest.Set（数字发成 JSON 数字）",
            Encoding.UTF8.GetString(Payloads.Build(new RenderRequest("BOX", LabelFormat.Pdf).Set("qty", schema.Sample["qty"]))).Contains("\"qty\":50"));
        check("响应结构不对（根不是对象）报 FormatException", Throws<FormatException>(() => Responses.ToSchema(JsonText.Parse("[1]"))));

        var sources = Responses.ToSchema(JsonText.Parse("""{"fields":[{"key":"input","source":"input"},{"key":"fixed","source":"template","default":0},{"key":"old","auto":"now"}]}"""));
        check("字段来源映射兼容旧响应，模板提供和旧自动值不要求接口填写", sources.Fields[0] is { Source: "input", RequiresInput: true }
            && sources.Fields[1] is { Source: "template", Default: 0m, RequiresInput: false }
            && sources.Fields[2] is { Source: null, RequiresInput: false });

        var error = Responses.ToError(JsonText.Parse("{\"code\":\"FIELD_REQUIRED\",\"message\":\"缺字段\",\"errors\":[{\"index\":1,\"field\":\"boxNo\",\"code\":\"FIELD_REQUIRED\"}],\"traceId\":\"00-x\"}"));
        check("错误响应映射：错误码、说明、逐条错误、跟踪 ID",
            error is { Code: "FIELD_REQUIRED", Message: "缺字段", TraceId: "00-x" } && error.Errors is [{ Index: 1, Field: "boxNo", Code: "FIELD_REQUIRED" }]);
        check("错误响应不是对象时当作没有错误体（按状态码处理）", Responses.ToError(JsonText.Parse("\"html\"")) == null);
    }

    /// <summary>
    /// 执行一个操作，看它是否抛出指定类型的异常。
    /// </summary>
    /// <typeparam name="T">期望的异常类型。</typeparam>
    /// <param name="action">操作。</param>
    /// <returns>抛出 <typeparamref name="T"/> 时为 true。</returns>
    private static bool Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }
}
