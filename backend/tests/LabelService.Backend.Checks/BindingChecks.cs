using LabelService.Contracts.Templates;
using LabelService.Rendering.Binding;

namespace LabelService.Backend.Checks;

/// <summary>
/// 绑定替换：{{key}}、{{key:format}}、格式优先级、未定义字段。
/// </summary>
internal static class BindingChecks
{
    public static void Run(Action<string, bool> check)
    {
        var fields = new[]
        {
            new FieldDefinition { Key = "batchNo" },
            new FieldDefinition { Key = "qty", Type = FieldType.Number, Format = "0" },
            new FieldDefinition { Key = "price", Type = FieldType.Number },
            new FieldDefinition { Key = "prodDate", Type = FieldType.Date, Format = "yyyy-MM-dd" },
            new FieldDefinition { Key = "when", Type = FieldType.Date },
            new FieldDefinition { Key = "note", Required = false },
        }.ToDictionary(field => field.Key);
        var values = new Dictionary<string, object?>
        {
            ["batchNo"] = "20261008-A",
            ["qty"] = 50m,
            ["price"] = 12.5m,
            ["prodDate"] = new DateTime(2026, 10, 8),
            ["when"] = new DateTime(2026, 10, 8, 14, 30, 0),
            ["note"] = null,
        };
        string Resolve(string text, List<string>? warnings = null) => BindingResolver.Resolve(text, values, fields, warnings);

        const string plain = "成品箱标";
        check("没有绑定时原样返回，不分配新字符串", ReferenceEquals(Resolve(plain), plain));
        check("{{key}} 替换成字段值", Resolve("批次：{{batchNo}}") == "批次：20261008-A");
        check("字段定义的格式生效", Resolve("{{qty}} PCS") == "50 PCS");
        check("绑定里的格式优先", Resolve("{{price:0.00}}") == "12.50");
        check("日期绑定格式", Resolve("{{prodDate:yyyy/MM/dd}}") == "2026/10/08");
        check("日期时间默认格式", Resolve("{{when}}") == "2026-10-08 14:30:00");
        check("花括号内允许空格", Resolve("{{ batchNo }}") == "20261008-A");
        check("可选字段没传时替换为空", Resolve("[{{note}}]") == "[]");
        var warnings = new List<string>();
        check("未定义字段替换为空并给出警告", Resolve("A{{missing}}B", warnings) == "AB" && warnings.SequenceEqual(["BINDING_UNKNOWN:missing"]));
        check("非法格式退回默认格式，不抛异常", BindingResolver.FormatValue(new DateTime(2026, 10, 8), "%") == "2026-10-08");
        check("FindKeys 去重并保持顺序", BindingResolver.FindKeys("{{a}}-{{b:0}}-{{a}}").SequenceEqual(["a", "b"]));
    }
}
