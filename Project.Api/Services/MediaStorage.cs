using System.Security.Cryptography;
using System.Text;
using Project.Application.Services;

namespace Project.Api.Services;

/// <summary>To-be T14 (Q11a): nơi lưu file media — đổi sang object storage sau này chỉ cần cài lại interface này.</summary>
public interface IMediaStorage
{
    Task SaveAsync(Guid id, byte[] content, CancellationToken ct = default);
    /// <summary>Đường dẫn vật lý để phục vụ (Range); null nếu file không còn.</summary>
    string? PhysicalPath(Guid id);
}

public sealed class MediaSettings
{
    public required string RootPath { get; init; }
    public required byte[] SigningKey { get; init; }
    public required MediaLimits Limits { get; init; }

    /// <summary>
    /// QT14-4: Development — thư mục mặc định ../dacn-media-dev và khóa ký ngẫu nhiên mỗi lần chạy; môi trường khác — bắt buộc
    /// Media:RootPath và Media:SigningKey (≥ 32 byte), thiếu thì dừng khởi động. Thư mục không được nằm trong wwwroot.
    /// </summary>
    public static MediaSettings From(IConfiguration configuration, IHostEnvironment environment, string? webRootPath)
    {
        var section = configuration.GetSection("Media");
        var limits = (section.Get<MediaLimits>() ?? new MediaLimits()).Validate();
        var root = section["RootPath"];
        if (string.IsNullOrWhiteSpace(root))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Media:RootPath is required outside Development.");
            root = Path.Combine(environment.ContentRootPath, "..", "dacn-media-dev");
        }
        root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
        var webRoot = Path.GetFullPath(webRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (IsUnder(root, webRoot))
            throw new InvalidOperationException("Media:RootPath must be outside wwwroot.");
        var keyText = section["SigningKey"];
        byte[] key;
        if (string.IsNullOrWhiteSpace(keyText))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Media:SigningKey is required outside Development.");
            key = RandomNumberGenerator.GetBytes(32);
        }
        else key = Encoding.UTF8.GetBytes(keyText);
        if (key.Length < 32) throw new InvalidOperationException("Media:SigningKey must be at least 32 bytes.");
        return new MediaSettings { RootPath = root, SigningKey = key, Limits = limits };
    }

    private static bool IsUnder(string path, string parent)
    {
        var p = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var q = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return p.StartsWith(q, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Lưu trên đĩa: &lt;Root&gt;/&lt;2 ký tự đầu&gt;/&lt;Guid&gt; (không dùng tên client); ghi file tạm rồi đổi tên.</summary>
public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _root;
    public LocalMediaStorage(MediaSettings settings) { _root = settings.RootPath; }

    private string PathOf(Guid id)
    {
        var name = id.ToString("N");
        return Path.Combine(_root, name[..2], name);
    }

    public async Task SaveAsync(Guid id, byte[] content, CancellationToken ct = default)
    {
        var path = PathOf(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, content, ct);
            File.Move(temp, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path)) { /* ghi song song cùng id — đã có */ }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public string? PhysicalPath(Guid id)
    {
        var path = PathOf(id);
        return File.Exists(path) ? path : null;
    }
}
