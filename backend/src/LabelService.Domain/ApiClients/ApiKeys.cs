using System.Security.Cryptography;
using System.Text;

namespace LabelService.Domain.ApiClients;

/// <summary>
/// API Key 的生成和哈希。库里和配置里只存 Key 的 SHA-256，原文只在新建和重置时给调用方看一次。
/// </summary>
public static class ApiKeys
{
    /// <summary>
    /// 后台列表里显示的 Key 前缀长度（如 lbl_live_3Fk9），只用来辨认是哪把 Key。
    /// </summary>
    public const int PrefixLength = 13;

    /// <summary>
    /// 随机部分用的字符：数字和大小写字母。
    /// </summary>
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 计算 Key 的 SHA-256（小写十六进制）。
    /// </summary>
    /// <param name="apiKey">Key 原文。</param>
    /// <returns>64 位十六进制字符串。</returns>
    public static string Hash(string apiKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>
    /// 生成一把新 Key：正式环境以 <c>lbl_live_</c> 开头，其他环境 <c>lbl_test_</c>，后面 32 位随机字母数字（约 190 位熵）。
    /// </summary>
    /// <param name="production">是否正式环境。</param>
    /// <returns>Key 原文。</returns>
    public static string Generate(bool production)
    {
        var prefix = production ? "lbl_live_" : "lbl_test_";
        return prefix + RandomNumberGenerator.GetString(Alphabet, 32);
    }

    /// <summary>
    /// Key 的前缀，后台列表里显示。
    /// </summary>
    /// <param name="apiKey">Key 原文。</param>
    /// <returns>前 <see cref="PrefixLength"/> 个字符。</returns>
    public static string Prefix(string apiKey) => apiKey[..PrefixLength];
}
