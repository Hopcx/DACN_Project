using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

public sealed record ReviewFlagRequest(string? SourceKind, Guid SourceId, Guid QuestionId);

/// <summary>
/// To-be T8: đánh dấu câu để xem lại, lưu ở server cho lượt thi ("exam") và phiên luyện tập ("practice").
/// Chỉ chủ nguồn (lượt thi: cùng điều kiện chủ lượt như GET /web/attempts/{id}; phiên: UserId) — khác → 404.
/// Được đánh dấu mọi lúc chủ mở được nguồn (đang làm, đã nộp, xem lại). PUT/DELETE idempotent. Không đụng luồng thi.
/// </summary>
[ApiController]
[Authorize(Policy = "Student")]
[Route("web/review-flags")]
public class ReviewFlagController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ProjectDACNDbContext _db;
    public ReviewFlagController(ProjectDACNDbContext db) => _db = db;

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // Snapshot JSON của nguồn nếu người gọi là chủ (lượt thi: còn là thành viên đã duyệt của lớp đang hoạt động)
    private IQueryable<string> SourceJson(string kind, Guid sourceId, Guid userId) => kind == ReviewFlagRules.Exam
        ? _db.ExamAttempts.AsNoTracking().Where(x => x.Id == sourceId && x.UserId == userId &&
                _db.ClassExamSchedules.Any(link => link.ExamScheduleId == x.ExamScheduleId && link.Class.Status == 1 &&
                    link.Class.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)))
            .Select(x => x.SnapshotJson)
        : _db.PracticeSessions.AsNoTracking().Where(x => x.Id == sourceId && x.UserId == userId).Select(x => x.SnapshotJson);

    // Snapshot của nguồn nếu người gọi là chủ; không có → null (404)
    private async Task<AttemptSnapshot?> SourceSnapshotAsync(string kind, Guid sourceId, Guid userId)
    {
        var json = await SourceJson(kind, sourceId, userId).FirstOrDefaultAsync();
        return json == null ? null : JsonSerializer.Deserialize<AttemptSnapshot>(json, Json);
    }

    // Tổng kiểm (nợ T8 N6): chỉ cần biết nguồn còn và thuộc người gọi — không kéo/giải mã snapshot
    private Task<bool> SourceExistsAsync(string kind, Guid sourceId, Guid userId) => SourceJson(kind, sourceId, userId).AnyAsync();

    private IQueryable<ReviewFlag> Flags(Guid userId, string kind, Guid sourceId) =>
        _db.ReviewFlags.Where(x => x.UserId == userId && x.SourceKind == kind && x.SourceId == sourceId);

    private (string? Kind, IActionResult? Error) Validate(string? sourceKind, Guid sourceId)
    {
        var kind = ReviewFlagRules.NormalizeKind(sourceKind);
        if (kind == null) return (null, BadRequest(ApiResponse<string>.Fail("sourceKind chỉ nhận 'exam' hoặc 'practice'.")));
        if (sourceId == Guid.Empty) return (null, BadRequest(ApiResponse<string>.Fail("sourceId không hợp lệ.")));
        return (kind, null);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? sourceKind, [FromQuery] Guid sourceId)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var (kind, error) = Validate(sourceKind, sourceId); if (error != null) return error;
        var snapshot = await SourceSnapshotAsync(kind!, sourceId, userId.Value);
        if (snapshot == null) return NotFound(ApiResponse<string>.Fail("REVIEW_FLAG_SOURCE_NOT_FOUND"));
        var ids = await Flags(userId.Value, kind!, sourceId).Select(x => x.SnapshotQuestionId).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { sourceKind = kind, sourceId, questionIds = ReviewFlagRules.InSnapshotOrder(snapshot, ids) }));
    }

    [HttpPut]
    public async Task<IActionResult> Flag([FromBody] ReviewFlagRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var (kind, error) = Validate(request.SourceKind, request.SourceId); if (error != null) return error;
        // Tổng kiểm (nợ T8 N4): phiên luyện tập có thể bị bỏ (xóa cùng cờ) đúng lúc đang đánh dấu. Lấy app lock theo học viên
        // như DELETE /web/student/practice/{id} để hai thao tác xếp hàng: kiểm nguồn + ghi cờ thấy đúng trạng thái sau khi
        // bỏ phiên, không phụ thuộc mức cô lập (kể cả khi bật RCSI). Lượt thi không bao giờ bị xóa nên không cần khóa.
        await using var tx = kind == ReviewFlagRules.Practice ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        if (tx != null) await PracticeFinalizer.LockAsync(_db, userId.Value);
        var snapshot = await SourceSnapshotAsync(kind!, request.SourceId, userId.Value);
        if (snapshot == null) return NotFound(ApiResponse<string>.Fail("REVIEW_FLAG_SOURCE_NOT_FOUND"));
        if (!ReviewFlagRules.HasQuestion(snapshot, request.QuestionId))
            return BadRequest(ApiResponse<string>.Fail("REVIEW_FLAG_QUESTION_INVALID"));
        if (!await Flags(userId.Value, kind!, request.SourceId).AnyAsync(x => x.SnapshotQuestionId == request.QuestionId))
        {
            _db.ReviewFlags.Add(new ReviewFlag
            {
                UserId = userId.Value, SourceKind = kind!, SourceId = request.SourceId,
                SnapshotQuestionId = request.QuestionId, CreatedAtUtc = DateTime.UtcNow
            });
            // Hai request đánh dấu lượt thi cùng lúc: PK chặn bản thứ hai — coi như đã đánh dấu (idempotent).
            // Lỗi trùng khóa chỉ hủy câu lệnh, không hủy transaction (XACT_ABORT tắt).
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 }) { }
        }
        if (tx != null) await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { sourceKind = kind, sourceId = request.SourceId, questionId = request.QuestionId, flagged = true }));
    }

    [HttpDelete]
    public async Task<IActionResult> Unflag([FromQuery] string? sourceKind, [FromQuery] Guid sourceId, [FromQuery] Guid questionId)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var (kind, error) = Validate(sourceKind, sourceId); if (error != null) return error;
        if (!await SourceExistsAsync(kind!, sourceId, userId.Value))
            return NotFound(ApiResponse<string>.Fail("REVIEW_FLAG_SOURCE_NOT_FOUND"));
        await Flags(userId.Value, kind!, sourceId).Where(x => x.SnapshotQuestionId == questionId).ExecuteDeleteAsync();
        return Ok(ApiResponse<object>.Ok(new { sourceKind = kind, sourceId, questionId, flagged = false }));
    }
}
