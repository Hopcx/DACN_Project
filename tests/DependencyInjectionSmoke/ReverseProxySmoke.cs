using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Project.Api.Services;

/// <summary>
/// T20 (QT20-2): sau Caddy trong Docker — IP thật và https chỉ lấy từ proxy thuộc mạng tin cậy; header giả từ nơi khác bị bỏ qua.
/// Chạy đúng ForwardedHeadersMiddleware của ASP.NET với cấu hình sinh từ ReverseProxySettings.
/// </summary>
internal static class ReverseProxySmoke
{
    public static async Task Run()
    {
        var production = new HostingEnvironment { EnvironmentName = Environments.Production };
        var development = new HostingEnvironment { EnvironmentName = Environments.Development };

        // Tắt (mặc định) → không đọc header
        if (ReverseProxySettings.From(Config(), production).Enabled) throw new Exception("Proxy must be off by default.");
        // Bật mà thiếu mạng tin cậy → Production dừng khởi động; CIDR sai → dừng
        Throws(() => ReverseProxySettings.From(Config(("Proxy:Enabled", "true")), production), "missing KnownNetworks");
        foreach (var bad in new[] { "172.28.0.0", "172.28.0.0/33", "abc/16", "::1/129" })
            Throws(() => ReverseProxySettings.From(Config(("Proxy:Enabled", "true"), ("Proxy:KnownNetworks:0", bad)), production), $"bad CIDR {bad}");
        if (!ReverseProxySettings.From(Config(("Proxy:Enabled", "true")), development).Enabled) throw new Exception("Development may omit networks.");

        var settings = ReverseProxySettings.From(Config(("Proxy:Enabled", "true"), ("Proxy:KnownNetworks:0", "172.28.0.0/16")), production);
        // Proxy tin cậy (172.28.0.5) chuyển tiếp người dùng 203.0.113.7 qua https
        var trusted = await Apply(settings, "172.28.0.5", "203.0.113.7", "https");
        Expect($"{trusted.Connection.RemoteIpAddress}|{trusted.Request.Scheme}", "203.0.113.7|https", "trusted proxy");
        // Người dùng tự gửi X-Forwarded-For giả qua proxy: chỉ lấy địa chỉ sát proxy nhất (ForwardLimit = 1)
        var chained = await Apply(settings, "172.28.0.5", "1.2.3.4, 198.51.100.9", "https");
        Expect($"{chained.Connection.RemoteIpAddress}", "198.51.100.9", "spoofed chain");
        // Gọi thẳng từ ngoài mạng tin cậy kèm header giả → bỏ qua
        var outsider = await Apply(settings, "8.8.8.8", "203.0.113.7", "https");
        Expect($"{outsider.Connection.RemoteIpAddress}|{outsider.Request.Scheme}", "8.8.8.8|http", "untrusted source");
        // Loopback không còn được tin mặc định
        var loopback = await Apply(settings, "127.0.0.1", "203.0.113.7", "https");
        Expect($"{loopback.Connection.RemoteIpAddress}", "127.0.0.1", "loopback not trusted");
        Console.WriteLine("REVERSE_PROXY_FORWARDED_HEADERS_TRUSTED_NETWORKS_ONLY=PASS (isolated; no SQL/API)");
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();

    private static async Task<HttpContext> Apply(ReverseProxySettings settings, string remote, string forwardedFor, string proto)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = proto;
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(settings.ForwardedOptions()));
        await middleware.Invoke(context);
        return context;
    }

    private static void Expect(string actual, string expected, string name)
    {
        if (actual != expected) throw new Exception($"REVERSE_PROXY {name}: expected {expected}, got {actual}");
    }

    private static void Throws(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception($"REVERSE_PROXY {name}: expected startup failure");
    }
}
