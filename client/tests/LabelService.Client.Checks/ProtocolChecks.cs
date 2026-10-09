using LabelService.Client.Internal;

namespace LabelService.Client.Checks;

/// <summary>
/// 协议解析：按 docs/contracts.md 的写法解析 Server-Timing。
/// </summary>
internal static class ProtocolChecks
{
    public static void Run(Action<string, bool> check)
    {
        // docs/contracts.md 里的示例原文。
        var parsed = ServerTimingParser.Parse("auth;dur=0.3, tpl;dur=0.1, validate;dur=0.6, layout;dur=4.2, draw;dur=18.5, encode;dur=21.7, total;dur=46.1");
        check("解析契约文档里的 Server-Timing 示例", parsed.Count == 7 && parsed["auth"] == 0.3 && parsed["total"] == 46.1);
        var tolerant = ServerTimingParser.Parse("cache;desc=\"hit\", draw;dur=abc, , encode;dur=2.5;desc=png");
        check("Server-Timing 里格式不对的项跳过，不抛异常", tolerant.Count == 1 && tolerant["encode"] == 2.5);
        check("没有 Server-Timing 头时返回空结果", ServerTimingParser.Parse(null).Count == 0);
    }
}
