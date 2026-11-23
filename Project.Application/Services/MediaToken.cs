using System.Security.Cryptography;
using System.Text;

namespace Project.Application.Services;

/// <summary>
/// To-be T14 (Q11b): token cho URL ký tạm <c>/web/media/{id}?t=…</c>. Dạng <c>{userId:N}.{exp unix}.{base64url HMAC-SHA256}</c>,
/// chữ ký phủ "mediaId|userId|exp" → không dùng lại được cho media khác. So sánh chữ ký bằng thời gian hằng.
/// Token không chứa bí mật; khóa ký nằm ở server (<c>Media:SigningKey</c>).
/// </summary>
public static class MediaToken
{
    public static string Sign(byte[] key, Guid mediaId, Guid userId, DateTimeOffset expiresAt)
    {
        var exp = expiresAt.ToUnixTimeSeconds();
        return $"{userId:N}.{exp}.{Mac(key, mediaId, userId, exp)}";
    }

    /// <summary>Trả userId khi token hợp lệ cho đúng media và chưa hết hạn; ngược lại null.</summary>
    public static Guid? Verify(byte[] key, Guid mediaId, string? token, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var userId) || !long.TryParse(parts[1], out var exp)) return null;
        if (now.ToUnixTimeSeconds() > exp) return null;
        var expected = Encoding.ASCII.GetBytes(Mac(key, mediaId, userId, exp));
        var actual = Encoding.ASCII.GetBytes(parts[2]);
        return CryptographicOperations.FixedTimeEquals(expected, actual) ? userId : null;
    }

    private static string Mac(byte[] key, Guid mediaId, Guid userId, long exp)
    {
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{mediaId:N}|{userId:N}|{exp}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
