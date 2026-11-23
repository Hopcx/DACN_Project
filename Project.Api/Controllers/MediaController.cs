using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

/// <summary>
/// To-be T14 (Q11, Q12): tải lên media (người có quyền câu hỏi) và phục vụ media bằng URL ký tạm (Q11b).
/// Loại file xác định bằng magic bytes; Content-Type theo loại đã phát hiện; không dùng tên file client làm đường dẫn.
/// </summary>
[ApiController]
[Authorize]   // mặc định cần đăng nhập; riêng GET {id} là AllowAnonymous và tự kiểm token ký tạm
[Route("web/media")]
public sealed class MediaController : ControllerBase
{
    /// <summary>Trần của request multipart; giới hạn thật theo loại nằm ở <see cref="MediaLimits"/>.</summary>
    public const long RequestCap = 101L * 1024 * 1024;
    private readonly ProjectDACNDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly MediaSettings _settings;

    public MediaController(ProjectDACNDbContext db, IMediaStorage storage, MediaSettings settings)
    { _db = db; _storage = storage; _settings = settings; }

    [HttpPost]
    [Authorize(Policy = "QuestionManagement")]
    // Review T14: trần thân request đặt theo cấu hình ở middleware (Program.cs); đây chỉ là trần phân tích form
    [RequestFormLimits(MultipartBodyLengthLimit = RequestCap)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<string>.Fail("MEDIA_EMPTY"));
        var limits = _settings.Limits;
        if (file.Length > limits.MaxUploadBytes) return StatusCode(413, ApiResponse<string>.Fail("MEDIA_TOO_LARGE"));
        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        using (var memory = new MemoryStream((int)file.Length))
        {
            await stream.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }
        var inspection = MediaInspector.Inspect(bytes, limits);
        if (inspection.Error != null)
            return inspection.Error == "MEDIA_TOO_LARGE" ? StatusCode(413, ApiResponse<string>.Fail(inspection.Error))
                : BadRequest(ApiResponse<string>.Fail(inspection.Error));
        var type = inspection.Type!;
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // QT14-2: trùng nội dung → dùng lại bản đã có. Review T14: bản đã tắt thì bật lại; file vật lý mất thì ghi lại;
        // không trả tên gốc của người tải trước (originalName = tên file của lần tải này).
        // Review T15: khóa dòng theo SHA (Admin xóa mồ côi cũng khóa dòng này) và làm mới UploadedAtUtc → bản dùng lại được
        // luật "chưa đủ 24 giờ" bảo vệ cho tới khi giáo viên gắn vào câu; Admin xóa trước thì không thấy dòng → tạo bản mới.
        await using (var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct))
        {
            var existing = await _db.MediaFiles.FromSqlInterpolated($"SELECT * FROM dbo.MediaFiles WITH (UPDLOCK, HOLDLOCK) WHERE Sha256 = {sha}")
                .FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                if (_storage.PhysicalPath(existing.Id) == null) await _storage.SaveAsync(existing.Id, bytes, ct);
                existing.Status = 1;
                existing.UploadedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Ok(ApiResponse<object>.Ok(View(existing, true, userId, SafeName(file.FileName))));
            }
            await tx.CommitAsync(ct);
        }
        var row = new MediaFile
        {
            Id = Guid.NewGuid(), Kind = type.Kind, ContentType = type.ContentType, Extension = type.Extension, SizeBytes = bytes.LongLength,
            Sha256 = sha, Width = inspection.Width, Height = inspection.Height, OriginalName = SafeName(file.FileName),
            UploadedBy = userId, UploadedAtUtc = DateTime.UtcNow, Status = 1
        };
        await _storage.SaveAsync(row.Id, bytes, ct);
        _db.MediaFiles.Add(row);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            // Tải song song cùng nội dung: bản kia thắng; file vừa ghi là mồ côi (Q15: không xóa tự động, Admin dọn ở T15)
            _db.ChangeTracker.Clear();
            var winner = await _db.MediaFiles.AsNoTracking().FirstAsync(x => x.Sha256 == sha, ct);
            return Ok(ApiResponse<object>.Ok(View(winner, true, userId)));
        }
        return Created($"/web/media/{row.Id}", ApiResponse<object>.Ok(View(row, false, userId)));
    }

    /// <summary>Phục vụ media qua URL ký tạm; kiểm token ở mọi request (kể cả từng request Range).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? t, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        // Review T14: file phục vụ không chạy được script dù trình duyệt mở trực tiếp; chỉ cùng site được nhúng
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-site";
        if (string.IsNullOrEmpty(t)) return Unauthorized(ApiResponse<string>.Fail("MEDIA_TOKEN_REQUIRED"));
        if (MediaToken.Verify(_settings.SigningKey, id, t, DateTimeOffset.UtcNow) == null)
            return StatusCode(403, ApiResponse<string>.Fail("MEDIA_TOKEN_INVALID"));
        var row = await _db.MediaFiles.AsNoTracking().Where(x => x.Id == id && x.Status == 1)
            .Select(x => new { x.ContentType }).FirstOrDefaultAsync(ct);
        var path = row == null ? null : _storage.PhysicalPath(id);
        if (row == null || path == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_FOUND"));
        Response.Headers.ContentDisposition = "inline";
        return PhysicalFile(path, row.ContentType, enableRangeProcessing: true);
    }

    private object View(MediaFile x, bool duplicate, Guid userId, string? uploadName = null) => new
    {
        id = x.Id, kind = x.Kind, contentType = x.ContentType, sizeBytes = x.SizeBytes, width = x.Width, height = x.Height,
        originalName = uploadName ?? x.OriginalName, duplicate, url = MediaUrls.Url(_settings, x.Id, userId)
    };

    private static string SafeName(string? name)
    {
        var value = Path.GetFileName(name ?? string.Empty).Trim();
        value = new string(value.Where(c => !char.IsControl(c)).ToArray());
        return value.Length == 0 ? "media" : value.Length > 255 ? value[..255] : value;
    }
}

public static class MediaUrls
{
    /// <summary>URL tương đối (cùng origin với API) có token ký tạm cho đúng người gọi và media.</summary>
    public static string Url(MediaSettings settings, Guid mediaId, Guid userId) =>
        $"/web/media/{mediaId}?t={Uri.EscapeDataString(MediaToken.Sign(settings.SigningKey, mediaId, userId,
            DateTimeOffset.UtcNow.AddMinutes(settings.Limits.UrlMinutes)))}";
}
