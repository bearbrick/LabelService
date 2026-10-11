namespace LabelService.Application.Identity;

/// <summary>
/// 密码哈希（ADR 0006）。基础设施层用 PBKDF2-SHA256 实现。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 账号不存在时拿来核对的哈希，让响应时间和账号存在时一样，不暴露账号是否存在。
    /// </summary>
    string DummyHash
    {
        get;
    }

    /// <summary>
    /// 计算密码哈希（随机盐）。
    /// </summary>
    /// <param name="password">密码原文。</param>
    /// <returns>可以存进库的哈希字符串。</returns>
    string Hash(string password);

    /// <summary>
    /// 核对密码，用固定时间比较防止时序攻击。
    /// </summary>
    /// <param name="password">输入的密码。</param>
    /// <param name="storedHash">库里存的哈希。</param>
    /// <returns>一致时为 true；哈希格式不对时为 false。</returns>
    bool Verify(string password, string storedHash);

    /// <summary>
    /// 生成临时密码（管理员重置密码、首次启动时用）：保证有字母和数字，用户首次登录后必须改。
    /// </summary>
    /// <returns>临时密码原文。</returns>
    string GenerateTemporary();
}
