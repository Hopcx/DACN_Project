using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using ProxyNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Project.Api.Services;

/// <summary>
/// T20 (QT20-2): chạy sau reverse proxy (Caddy trong Docker). Khi <c>Proxy:Enabled = true</c>:
/// - Đọc X-Forwarded-For / X-Forwarded-Proto **chỉ** từ proxy nằm trong <c>Proxy:KnownNetworks</c> (CIDR, vd. mạng Docker),
///   để rate limit theo IP thật của người dùng (không dồn mọi người vào IP của proxy) và Request.Scheme = https.
///   Header từ nơi khác bị bỏ qua (chống giả IP).
/// - Không bật UseHttpsRedirection: proxy đã ép HTTPS; healthcheck trong container gọi HTTP trực tiếp.
/// Ngoài Development mà bật proxy nhưng không khai báo mạng tin cậy → dừng khởi động (không tin mọi nguồn).
/// </summary>
public sealed class ReverseProxySettings
{
    public bool Enabled { get; init; }
    public IReadOnlyList<ProxyNetwork> KnownNetworks { get; init; } = [];

    public static ReverseProxySettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection("Proxy");
        if (!section.GetValue("Enabled", false)) return new ReverseProxySettings();
        var networks = new List<ProxyNetwork>();
        foreach (var text in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
        {
            var parts = text.Trim().Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix) || !int.TryParse(parts[1], out var length) ||
                length < 0 || length > (prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32))
                throw new InvalidOperationException($"Proxy:KnownNetworks có giá trị không hợp lệ: '{text}' (cần CIDR, vd. 172.28.0.0/16).");
            networks.Add(new ProxyNetwork(prefix, length));
        }
        if (networks.Count == 0 && !environment.IsDevelopment())
            throw new InvalidOperationException("Proxy:Enabled = true nhưng chưa khai báo Proxy:KnownNetworks.");
        return new ReverseProxySettings { Enabled = true, KnownNetworks = networks };
    }

    public ForwardedHeadersOptions ForwardedOptions()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            // Một tầng proxy (Caddy): chỉ lấy địa chỉ sát proxy nhất trong X-Forwarded-For
            ForwardLimit = 1,
        };
        // Bỏ mặc định (chỉ loopback) — chỉ tin đúng các mạng đã khai báo
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var network in KnownNetworks) options.KnownNetworks.Add(network);
        return options;
    }
}
