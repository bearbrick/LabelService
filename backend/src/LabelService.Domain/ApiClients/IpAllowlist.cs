using System.Net;
using System.Net.Sockets;

namespace LabelService.Domain.ApiClients;

/// <summary>
/// 调用方的 IP 白名单：CIDR 网段（10.20.1.0/24）或单个地址，逗号分隔；空表示不限。
/// </summary>
public static class IpAllowlist
{
    /// <summary>
    /// 解析白名单。
    /// </summary>
    /// <param name="value">逗号分隔的网段和地址。</param>
    /// <returns>网段列表；单个地址当作 /32（IPv6 为 /128）。</returns>
    /// <exception cref="FormatException">有一项不是合法的地址或网段。</exception>
    public static IReadOnlyList<IPNetwork> Parse(string value)
    {
        var networks = new List<IPNetwork>();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPNetwork.TryParse(item, out var network))
            {
                networks.Add(network);
            }
            else if (IPAddress.TryParse(item, out var address))
            {
                networks.Add(new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                throw new FormatException($"“{item}”不是合法的 IP 地址或网段");
            }
        }

        return networks;
    }

    /// <summary>
    /// 规范化用户输入：中英文逗号、换行、空格都当分隔符，去掉空项后用英文逗号连接。
    /// </summary>
    /// <param name="value">后台表单里填的白名单。</param>
    /// <returns>如 “10.20.1.0/24,192.168.1.5”。</returns>
    public static string Normalize(string value) =>
        string.Join(",", value.Split([',', '，', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// 来源地址是否在白名单里。白名单为空时总是允许。
    /// </summary>
    /// <param name="networks">解析好的白名单。</param>
    /// <param name="address">来源地址；未知时为 null（有白名单时不允许）。</param>
    /// <returns>允许时为 true。</returns>
    public static bool Allows(IReadOnlyList<IPNetwork> networks, IPAddress? address)
    {
        if (networks.Count == 0)
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        // 双栈监听时 IPv4 客户端的地址是 IPv4 映射的 IPv6 地址，先还原成 IPv4 再比较。
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return networks.Any(network => network.Contains(normalized));
    }
}
