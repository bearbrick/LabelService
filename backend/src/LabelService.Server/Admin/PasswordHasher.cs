using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LabelService.Server.Admin;

/// <summary>
/// 密码哈希：PBKDF2-SHA256，随机盐，格式 <c>pbkdf2-sha256$迭代次数$盐$哈希</c>（ADR 0006）。只用 .NET 自带的实现。
/// </summary>
internal static class PasswordHasher
{
    /// <summary>
    /// 迭代次数（OWASP 2023 对 PBKDF2-SHA256 的建议值）。存在哈希里，以后调高不影响已有密码。
    /// </summary>
    public const int Iterations = 210_000;

    private const string Scheme = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private static readonly Lazy<string> DummyHash = new(() => Hash("timing-placeholder-" + Guid.NewGuid()));

    /// <summary>
    /// 账号不存在时拿来核对的哈希，让响应时间和账号存在时一样。
    /// </summary>
    public static string Dummy => DummyHash.Value;

    /// <summary>
    /// 密码规则：至少 8 位，同时有字母和数字。返回不符合时的说明，符合时返回 null。
    /// </summary>
    public static string? Validate(string password) =>
        password.Length < 8 || !password.Any(char.IsAsciiLetter) || !password.Any(char.IsAsciiDigit)
            ? "密码至少 8 位，要同时有字母和数字"
            : null;

    /// <summary>
    /// 计算密码哈希。
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Scheme, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// 核对密码，用固定时间比较防止时序攻击。哈希格式不对时返回 false。
    /// </summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 生成临时密码（管理员重置密码、首次启动时用）：12 位，保证有字母和数字。
    /// </summary>
    public static string Temporary()
    {
        const string Letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string Digits = "23456789";
        return RandomNumberGenerator.GetString(Letters, 8) + RandomNumberGenerator.GetString(Digits, 4);
    }
}
