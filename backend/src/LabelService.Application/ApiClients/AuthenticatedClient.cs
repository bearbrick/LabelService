using System.Net;
using LabelService.Application.Templates;
using LabelService.Domain.ApiClients;

namespace LabelService.Application.ApiClients;

/// <summary>
/// 已识别的调用方（开放接口的请求凭 API Key 识别出来的）。
/// </summary>
/// <param name="Id">数据库里的调用方 ID；配置文件里的调用方为 null。</param>
/// <param name="Name">调用方名称，写进调用日志和指标。</param>
/// <param name="AllBusinesses">能调用全部模板。</param>
/// <param name="Businesses">能调用的业务分组 ID。</param>
/// <param name="Templates">能调用的模板编码（配置文件里的调用方用）。</param>
/// <param name="Networks">IP 白名单；空表示不限。</param>
public sealed record AuthenticatedClient(
    int? Id,
    string Name,
    bool AllBusinesses,
    IReadOnlySet<int> Businesses,
    IReadOnlySet<string> Templates,
    IReadOnlyList<IPNetwork> Networks)
{
    /// <summary>
    /// 是否允许使用某个已发布的模板：全部、模板编码在列表里，或者模板所属的业务分组在授权范围里。
    /// </summary>
    /// <param name="template">已发布的模板。</param>
    /// <returns>允许时为 true。</returns>
    public bool CanUse(PublishedTemplate template) =>
        AllBusinesses
        || Templates.Contains(template.Template.Code)
        || (template.BusinessId is { } business && Businesses.Contains(business));

    /// <summary>
    /// 来源 IP 是否在白名单里；没设白名单时总是允许。
    /// </summary>
    /// <param name="address">来源地址。</param>
    /// <returns>允许时为 true。</returns>
    public bool AllowsIp(IPAddress? address) => IpAllowlist.Allows(Networks, address);
}
