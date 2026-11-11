using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Project.Api.Services;

/// <summary>
/// Giới hạn tần suất cho /web/auth/refresh và /web/auth/logout.
///
/// Trước đây chỉ phân vùng theo IP (20 lần/phút). Một phòng thi đi ra Internet qua chung một IP NAT
/// thì cả lớp dùng chung 20 lượt: khi nhiều học viên tải lại trang cùng lúc, refresh bị 429 và người dùng
/// bị đưa về trang đăng nhập. Nay:
/// - Policy "auth-session": phân vùng theo hash của refresh cookie (mỗi phiên trình duyệt một hạn mức);
///   request không có cookie vẫn phân vùng theo IP với hạn mức thấp như cũ.
/// - Trần theo IP (GlobalLimiter, chỉ áp cho hai đường dẫn trên) cao hơn nhiều, đủ cho một phòng thi,
///   vẫn chặn được việc dồn request với cookie ngẫu nhiên từ một máy.
/// Refresh token có 256 bit ngẫu nhiên và bị xoay vòng sau mỗi lần dùng, nên hạn mức theo token chủ yếu
/// chặn một phiên tự lặp vô hạn; dùng lại token cũ vẫn thu hồi cả họ token như trước.
/// </summary>
public static class SessionRateLimits
{
    public const string RefreshCookie = "__Secure-dacn-refresh";
    private static readonly string[] SessionPaths = ["/web/auth/refresh", "/web/auth/logout"];

    public sealed record Settings(int PerSessionPerMinute, int PerIpPerMinute, int NoCookiePerIpPerMinute);

    public static Settings Read(IConfiguration configuration) => new(
        configuration.GetValue("RateLimits:SessionPerTokenPerMinute", 10),
        configuration.GetValue("RateLimits:SessionPerIpPerMinute", 300),
        configuration.GetValue("RateLimits:SessionNoCookiePerIpPerMinute", 20));

    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions PerMinute(int permits) =>
        new() { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 };

    /// <summary>Khóa phân vùng của policy "auth-session" (tách riêng để kiểm thử).</summary>
    public static string SessionPartitionKey(HttpContext context) =>
        context.Request.Cookies.TryGetValue(RefreshCookie, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? "rt:" + AuthTokenTools.Hash(raw)
            : "ip:" + Ip(context);

    public static RateLimitPartition<string> SessionPolicy(HttpContext context, Settings settings)
    {
        var key = SessionPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(key,
            _ => PerMinute(key.StartsWith("rt:", StringComparison.Ordinal) ? settings.PerSessionPerMinute : settings.NoCookiePerIpPerMinute));
    }

    public static bool IsSessionPath(PathString path) =>
        SessionPaths.Any(x => path.Equals(x, StringComparison.OrdinalIgnoreCase));

    public static PartitionedRateLimiter<HttpContext> IpCeiling(Settings settings) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context => IsSessionPath(context.Request.Path)
            ? RateLimitPartition.GetFixedWindowLimiter("ip:" + Ip(context), _ => PerMinute(settings.PerIpPerMinute))
            : RateLimitPartition.GetNoLimiter("other"));
}
