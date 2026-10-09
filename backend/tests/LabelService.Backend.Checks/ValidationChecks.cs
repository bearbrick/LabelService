using System.Text.Json;
using LabelService.Contracts.Protocol;
using LabelService.Contracts.Templates;
using LabelService.Rendering.Validation;

namespace LabelService.Backend.Checks;

/// <summary>
/// 字段校验：必填、类型转换、批量序号、公共字段覆盖、图片。
/// </summary>
internal static class ValidationChecks
{
    public static void Run(string root, Action<string, bool> check)
    {
        var fields = Fixtures.LoadTemplate(root).Fields;
        FieldValidationResult Validate(string data, string? common = null, int? index = null) =>
            FieldValidator.Validate(fields, common is null ? null : Fixtures.Data(common), Fixtures.Data(data), index);
        string With(string key, string jsonValue) =>
            JsonSerializer.Serialize(new Dictionary<string, JsonElement>(Fixtures.Data(Fixtures.BoxLabelData)) { [key] = JsonDocument.Parse(jsonValue).RootElement });
        bool Fails(FieldValidationResult result, string field, string code) =>
            result.Errors.Any(error => error.Field == field && error.Code == code);

        var ok = Validate(Fixtures.BoxLabelData);
        check("样例数据通过校验", ok.IsValid);
        check("number 转成 decimal", ok.Values["qty"] is 50m);
        check("date 转成 DateTime", ok.Values["prodDate"] is DateTime date && date == new DateTime(2026, 10, 8));

        var batch = Validate("{}", common: """{"partName":"a","partNo":"b","qty":1,"batchNo":"c"}""", index: 1);
        check("批量缺字段的提示带条目序号（和设计书示例一致）",
            batch.Errors is [{ Index: 1, Field: "boxNo", Code: ErrorCodes.FieldRequired, Message: "第 2 条缺少必填字段 boxNo" }]);
        check("条目字段覆盖公共字段", Validate("""{"boxNo":"B"}""", common: With("boxNo", "\"A\"")).Values["boxNo"] as string == "B");
        check("数字可以用字符串传", Validate(With("qty", "\"50\"")).Values["qty"] is 50m);
        check("非数字返回 FIELD_TYPE_INVALID", Fails(Validate(With("qty", "\"abc\"")), "qty", ErrorCodes.FieldTypeInvalid));
        check("非 ISO 日期返回 FIELD_TYPE_INVALID", Fails(Validate(With("prodDate", "\"10/08/2026\"")), "prodDate", ErrorCodes.FieldTypeInvalid));
        check("带时区的日期保留钟面时间",
            Validate(With("prodDate", "\"2026-10-08T23:30:00+08:00\"")).Values["prodDate"] is DateTime zoned && zoned == new DateTime(2026, 10, 8, 23, 30, 0));
        check("空字符串视为没传", Fails(Validate(With("boxNo", "\"  \"")), "boxNo", ErrorCodes.FieldRequired));
        check("可选字段不传时为 null", Validate(With("prodDate", "null")) is { IsValid: true } optional && optional.Values["prodDate"] is null);
        check("多传的 key 忽略", Validate(With("extra", "123")).IsValid);
        check("对象不能当字符串", Fails(Validate(With("partName", "{}")), "partName", ErrorCodes.FieldTypeInvalid));

        var special = new[]
        {
            new FieldDefinition { Key = "logo", Type = FieldType.Image },
            new FieldDefinition { Key = "code", MaxLength = 3 },
            new FieldDefinition { Key = "line", Default = JsonDocument.Parse("\"A1\"").RootElement },
        };
        FieldValidationResult ValidateSpecial(string data) => FieldValidator.Validate(special, null, Fixtures.Data(data));
        var png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        var result = ValidateSpecial($$"""{"logo":"data:image/png;base64,{{png}}","code":"abc"}""");
        check("带 data URI 前缀的 PNG 解码成 byte[]", result.IsValid && result.Values["logo"] is byte[] { Length: 12 });
        check("没传的字段使用默认值", result.Values["line"] as string == "A1");
        check("不是图片的 base64 返回 IMAGE_INVALID",
            Fails(ValidateSpecial($$"""{"logo":"{{Convert.ToBase64String("hello"u8.ToArray())}}","code":"a"}"""), "logo", ErrorCodes.ImageInvalid));
        var huge = new byte[RenderLimits.MaxImageBytes + 1];
        huge[0] = 0x89;
        huge[1] = 0x50;
        huge[2] = 0x4E;
        huge[3] = 0x47;
        check("超过 1 MB 的图片返回 IMAGE_INVALID",
            Fails(ValidateSpecial($$"""{"logo":"{{Convert.ToBase64String(huge)}}","code":"a"}"""), "logo", ErrorCodes.ImageInvalid));
        check("超过最大长度返回 FIELD_TYPE_INVALID", Fails(ValidateSpecial($$"""{"logo":"{{png}}","code":"abcd"}"""), "code", ErrorCodes.FieldTypeInvalid));
    }
}
