using System.Net;
using System.Text.Json;

namespace LabelService.Backend.Checks;

/// <summary>
/// 调用方管理的业务范围（ADR 0006）：范围受限的“接口运维”只能把自己范围里的分组授权出去，
/// 看不到、改不了、重置不了授权超出自己范围的调用方，否则就能拿到一把能调别的业务模板的 Key。
/// </summary>
internal static class ClientScopeChecks
{
    /// <summary>
    /// 运行调用方业务范围的全部检查：建一个范围受限的接口运维，看它能授权、看到、修改哪些调用方。
    /// </summary>
    /// <param name="http">调用服务的客户端。</param>
    /// <param name="admin">已登录并改过密码的 admin 会话。</param>
    /// <param name="check">记录检查结果。</param>
    /// <returns>检查做完时完成。</returns>
    public static async Task RunAsync(HttpClient http, AdminSession admin, Action<string, bool> check)
    {
        var businesses = await admin.JsonAsync(HttpMethod.Get, "/api/admin/businesses");
        int IdOf(string code) => businesses.EnumerateArray().Single(item => item.GetProperty("code").GetString() == code).GetProperty("id").GetInt32();
        var packId = IdOf("PACK");
        var sampleId = IdOf("SAMPLE");
        var (ops, _) = await AdminAccountChecks.CreateUserAsync(http, admin, "zhouqiang", "接口运维", false, [packId]);

        check("范围受限的接口运维不能新建全部业务的调用方（403）",
            await StatusAsync(ops.PostAsync("/api/admin/clients", Client("越权全部", true, []))) == HttpStatusCode.Forbidden);
        check("也不能把自己范围外的分组授权给调用方（403）",
            await StatusAsync(ops.PostAsync("/api/admin/clients", Client("越权分组", false, [packId, sampleId]))) == HttpStatusCode.Forbidden);

        var own = await ops.JsonAsync(HttpMethod.Post, "/api/admin/clients", Client("包装线扫码枪", false, [packId]));
        var ownId = own.GetProperty("client").GetProperty("id").GetInt32();
        var wide = await admin.JsonAsync(HttpMethod.Post, "/api/admin/clients", Client("全业务 ERP", true, []));
        var wideId = wide.GetProperty("client").GetProperty("id").GetInt32();
        var other = await admin.JsonAsync(HttpMethod.Post, "/api/admin/clients", Client("样例系统", false, [sampleId]));
        var otherId = other.GetProperty("client").GetProperty("id").GetInt32();

        var listed = (await ops.JsonAsync(HttpMethod.Get, "/api/admin/clients")).EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToHashSet();
        var all = (await admin.JsonAsync(HttpMethod.Get, "/api/admin/clients")).EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToHashSet();
        check("范围内可以新建调用方；列表里只有授权完全在自己范围里的调用方，全部业务的用户看得到全部",
            listed.Contains(ownId) && !listed.Contains(wideId) && !listed.Contains(otherId)
            && all.IsSupersetOf([ownId, wideId, otherId]));

        var denied = new List<HttpStatusCode>();
        foreach (var id in new[] { wideId, otherId })
        {
            denied.Add(await StatusAsync(ops.PutAsync($"/api/admin/clients/{id}", Client("改名", false, [packId]))));
            denied.Add(await StatusAsync(ops.PostAsync($"/api/admin/clients/{id}/status", new
            {
                enabled = false
            })));
            denied.Add(await StatusAsync(ops.PostAsync($"/api/admin/clients/{id}/reset-key")));
        }

        var wideAfter = (await admin.JsonAsync(HttpMethod.Get, "/api/admin/clients")).EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == wideId);
        check("超出范围的调用方：修改、停用、重置 Key 都是 404，记录没有被改",
            denied.All(status => status == HttpStatusCode.NotFound)
            && wideAfter.GetProperty("name").GetString() == "全业务 ERP" && wideAfter.GetProperty("enabled").GetBoolean()
            && wideAfter.GetProperty("keyPrefix").GetString() == wide.GetProperty("client").GetProperty("keyPrefix").GetString());

        check("不能把自己的调用方改成全部业务或加上范围外的分组（403），范围内的修改、重置 Key 可以",
            await StatusAsync(ops.PutAsync($"/api/admin/clients/{ownId}", Client("包装线扫码枪", true, []))) == HttpStatusCode.Forbidden
            && await StatusAsync(ops.PutAsync($"/api/admin/clients/{ownId}", Client("包装线扫码枪", false, [packId, sampleId]))) == HttpStatusCode.Forbidden
            && await StatusAsync(ops.PutAsync($"/api/admin/clients/{ownId}", Client("包装线扫码枪 2 号", false, [packId]))) == HttpStatusCode.OK
            && await StatusAsync(ops.PostAsync($"/api/admin/clients/{ownId}/reset-key")) == HttpStatusCode.OK);

        foreach (var id in new[] { ownId, wideId, otherId })
        {
            await admin.PostAsync($"/api/admin/clients/{id}/status", new
            {
                enabled = false
            });
        }
    }

    /// <summary>
    /// 新建或修改调用方的请求体。
    /// </summary>
    /// <param name="name">调用方名称。</param>
    /// <param name="allBusinesses">授权全部业务分组。</param>
    /// <param name="businessIds">授权的业务分组 ID。</param>
    /// <returns>请求体（匿名对象）。</returns>
    private static object Client(string name, bool allBusinesses, int[] businessIds) => new
    {
        name,
        appId = "",
        description = "",
        allBusinesses,
        businessIds,
        ipAllowlist = ""
    };

    /// <summary>
    /// 等请求完成，只取状态码并释放响应。
    /// </summary>
    /// <param name="request">发出的请求。</param>
    /// <returns>HTTP 状态码。</returns>
    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }
}
