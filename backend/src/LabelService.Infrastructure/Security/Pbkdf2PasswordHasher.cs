using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LabelService.Application.Identity;

namespace LabelService.Infrastructure.Security;

/// <summary>
/// 密码哈希：PBKDF2-SHA256，随机盐，格式 <c>pbkdf2-sha256$迭代次数$盐$哈希</c>（ADR 0006）。只用 .NET 自带的实现。
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 迭代次数（OWASP 2023 对 PBKDF2-SHA256 的建议值）。存在哈希里，以后调高不影响已有密码。
    /// </summary>
    public const int Iterations = 210_000;

    /// <summary>
    /// 哈希格式的标识，存在哈希字符串的第一段。
    /// </summary>
    private const string Scheme = "pbkdf2-sha256";

    /// <summary>
    /// 盐的长度，字节。
    /// </summary>
    private const int SaltBytes = 16;

    /// <summary>
    /// 哈希的长度，字节。
    /// </summary>
    private const int HashBytes = 32;

    /// <summary>
    /// 账号不存在时拿来核对的哈希，第一次用到时计算。
    /// </summary>
    private readonly Lazy<string> dummyHash;

    /// <summary>
    /// 创建哈希器。
    /// </summary>
    public Pbkdf2PasswordHasher() => dummyHash = new(() => Hash("timing-placeholder-" + Guid.NewGuid()));

    /// <inheritdoc />
    public string DummyHash => dummyHash.Value;

    /// <inheritdoc />
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Scheme, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <inheritdoc />
    public bool Verify(string password, string storedHash)
    {
        // 格式：方案$迭代次数$盐$哈希，任何一段不对都按不一致处理。
        var parts = storedHash.Split('$');
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

    /// <inheritdoc />
    public string GenerateTemporary()
    {
        // 去掉容易看混的字符（i、l、o、0、1），8 个字母加 4 个数字。
        const string Letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string Digits = "23456789";
        return RandomNumberGenerator.GetString(Letters, 8) + RandomNumberGenerator.GetString(Digits, 4);
    }
}
