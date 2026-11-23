using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

/// <summary>
/// To-be T15 (Q11c, QT15-4): media mồ côi — không câu hỏi nào gắn (QuestionMedia) và không snapshot nào tham chiếu
/// (MediaReferences), tải lên ≥ 24 giờ. Không xóa tự động (Q15): Admin xóa tay (dòng + file), ghi audit.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/media")]
public sealed class AdminMediaController : ControllerBase
{
    public static readonly TimeSpan MinAge = TimeSpan.FromHours(24);
    private readonly ProjectDACNDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly MediaSettings _settings;
    private readonly AuditWriter _audit;

    public AdminMediaController(ProjectDACNDbContext db, IMediaStorage storage, MediaSettings settings, AuditWriter audit)
    { _db = db; _storage = storage; _settings = settings; _audit = audit; }

    private IQueryable<Domain.Entities.MediaFile> Orphans(DateTime cutoff) => _db.MediaFiles.AsNoTracking()
        .Where(m => m.UploadedAtUtc <= cutoff && !_db.QuestionMedia.Any(q => q.MediaId == m.Id) && !_db.MediaReferences.Any(r => r.MediaId == m.Id));

    [HttpGet("orphans")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest(ApiResponse<string>.Fail("INVALID_PAGE"));
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var adminId)) return Unauthorized();
        var cutoff = DateTime.UtcNow - MinAge;
        var query = Orphans(cutoff);
        var total = await query.CountAsync(ct);
        var totalBytes = await query.SumAsync(x => (long?)x.SizeBytes, ct) ?? 0;
        var rows = await query.OrderBy(x => x.UploadedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Kind, x.ContentType, x.SizeBytes, x.Width, x.Height, x.OriginalName, x.UploadedAtUtc, x.UploadedBy,
                Uploader = _db.Users.Where(u => u.Id == x.UploadedBy).Select(u => u.FullName + " (" + u.UserName + ")").FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            page, pageSize, total, totalBytes, minAgeHours = (int)MinAge.TotalHours,
            items = rows.Select(x => new
            {
                id = x.Id, kind = x.Kind, contentType = x.ContentType, sizeBytes = x.SizeBytes, width = x.Width, height = x.Height,
                originalName = x.OriginalName, uploadedAtUtc = DateTime.SpecifyKind(x.UploadedAtUtc, DateTimeKind.Utc), uploader = x.Uploader,
                url = MediaUrls.Url(_settings, x.Id, adminId)
            })
        }));
    }

    /// <summary>Xóa media mồ côi: kiểm lại trong transaction có khóa; còn tham chiếu/chưa đủ tuổi → 409. Xóa file sau khi commit.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await _db.MediaFiles.FromSqlInterpolated($"SELECT * FROM dbo.MediaFiles WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .FirstOrDefaultAsync(ct);
        if (row == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_FOUND"));
        if (await _db.QuestionMedia.AnyAsync(x => x.MediaId == id, ct) || await _db.MediaReferences.AnyAsync(x => x.MediaId == id, ct))
            return Conflict(ApiResponse<string>.Fail("MEDIA_IN_USE"));
        if (row.UploadedAtUtc > DateTime.UtcNow - MinAge) return Conflict(ApiResponse<string>.Fail("MEDIA_TOO_RECENT"));
        _db.MediaFiles.Remove(row);
        _audit.Add(User, "media.delete", "MediaFile", id.ToString(), $"Xóa media mồ côi {row.Kind} {row.OriginalName} ({row.SizeBytes} byte)");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _storage.Delete(id);   // sau commit: lỗi xóa file chỉ để lại file thừa, không làm mất dữ liệu đang dùng
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
