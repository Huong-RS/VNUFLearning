namespace VNUFLearning.Services;

/// <summary>
/// Hash mật khẩu bằng BCrypt. Hỗ trợ migrate dần từ plain-text:
/// nếu hash hiện tại không phải BCrypt, fallback so sánh plain-text rồi yêu cầu rehash.
/// </summary>
public static class PasswordHasher
{
    private const int WorkFactor = 11;

    public static string Hash(string plain) =>
        BCrypt.Net.BCrypt.HashPassword(plain, WorkFactor);

    public static bool IsBCryptHash(string? value) =>
        !string.IsNullOrEmpty(value) &&
        (value.StartsWith("$2a$") || value.StartsWith("$2b$") || value.StartsWith("$2y$"));

    /// <summary>
    /// Verify mật khẩu. Trả về (matched, needsRehash).
    /// - Nếu stored là BCrypt hash: dùng BCrypt.Verify.
    /// - Nếu stored là plain-text legacy: so sánh trực tiếp, đánh dấu cần rehash.
    /// </summary>
    public static (bool matched, bool needsRehash) Verify(string plain, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return (false, false);

        if (IsBCryptHash(stored))
        {
            try { return (BCrypt.Net.BCrypt.Verify(plain, stored), false); }
            catch { return (false, false); }
        }

        // Legacy plain-text (sẽ được rehash khi đăng nhập thành công)
        return (string.Equals(plain, stored, StringComparison.Ordinal), true);
    }
}
