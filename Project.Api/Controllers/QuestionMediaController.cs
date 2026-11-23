using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

/// <summary>
/// To-be T14 (QT14-3): đính kèm media cho câu hỏi — tối đa <c>Media:MaxPerQuestion</c> (5), trong đó ≤ <c>MaxAudioPerQuestion</c> (1) audio.
/// Như sửa câu hỏi: câu đã nằm trong đề/đã có bài làm/đã xóa thì khóa (409 QUESTION_LOCKED). Gắn/gỡ ghi audit.
/// </summary>
[ApiController]
[Authorize(Policy = "QuestionManagement")]
[Route("web/questions/{questionId:int}/media")]
public sealed class QuestionMediaController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly MediaSettings _settings;
    private readonly AuditWriter _audit;

    public QuestionMediaController(ProjectDACNDbContext db, MediaSettings settings, AuditWriter audit)
    { _db = db; _settings = settings; _audit = audit; }

    public sealed record AttachRequest(Guid MediaId);

    // Review T14: trả kèm `locked` để FE không cho tải lên khi câu bị khóa (tránh sinh file mồ côi)
    [HttpGet]
    public async Task<IActionResult> List(int questionId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await _db.Questions.AnyAsync(x => x.Id == questionId && x.Status != 255, ct)) return NotFound(ApiResponse<string>.Fail("QUESTION_NOT_FOUND"));
        return Ok(ApiResponse<object>.Ok(await ViewAsync(questionId, userId, ct)));
    }

    [HttpPost]
    public async Task<IActionResult> Attach(int questionId, [FromBody] AttachRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (await LockAndCheckAsync(questionId, ct) is { } denied) return denied;
        var media = await _db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.MediaId && x.Status == 1, ct);
        if (media == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_FOUND"));
        var current = await _db.QuestionMedia.Where(x => x.QuestionId == questionId)
            .Select(x => new { x.MediaId, x.SortOrder, x.Media.Kind }).ToListAsync(ct);
        if (current.Any(x => x.MediaId == media.Id)) return Conflict(ApiResponse<string>.Fail("MEDIA_ALREADY_ATTACHED"));
        var limits = _settings.Limits;
        if (current.Count >= limits.MaxPerQuestion) return Conflict(ApiResponse<string>.Fail("MEDIA_LIMIT_REACHED"));
        if (media.Kind == "audio" && current.Count(x => x.Kind == "audio") >= limits.MaxAudioPerQuestion)
            return Conflict(ApiResponse<string>.Fail("MEDIA_AUDIO_LIMIT_REACHED"));
        _db.QuestionMedia.Add(new QuestionMedia { QuestionId = questionId, MediaId = media.Id, SortOrder = current.Count == 0 ? 1 : current.Max(x => x.SortOrder) + 1 });
        _audit.Add(User, "question.media.attach", "Question", questionId.ToString(), $"Gắn {media.Kind} {media.Id} ({media.SizeBytes} byte)");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(await ViewAsync(questionId, userId, ct)));
    }

    [HttpDelete("{mediaId:guid}")]
    public async Task<IActionResult> Detach(int questionId, Guid mediaId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (await LockAndCheckAsync(questionId, ct) is { } denied) return denied;
        var link = await _db.QuestionMedia.FirstOrDefaultAsync(x => x.QuestionId == questionId && x.MediaId == mediaId, ct);
        if (link == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_ATTACHED"));
        _db.QuestionMedia.Remove(link);
        _audit.Add(User, "question.media.detach", "Question", questionId.ToString(), $"Gỡ media {mediaId}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(await ViewAsync(questionId, userId, ct)));
    }

    // Review T14: khóa dòng câu hỏi (UPDLOCK, HOLDLOCK) để mọi thao tác gắn/gỡ trên cùng câu chạy lần lượt — tránh deadlock
    // của khóa phạm vi Serializable khi hai request chèn cùng lúc. Điều kiện khóa giống sửa câu hỏi (SaveWithAnswersAsync).
    private async Task<IActionResult?> LockAndCheckAsync(int questionId, CancellationToken ct)
    {
        var status = await _db.Database.SqlQuery<byte?>(
            $"SELECT Status AS Value FROM dbo.Questions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {questionId}").ToListAsync(ct);
        if (status.Count == 0 || status[0] == 255) return NotFound(ApiResponse<string>.Fail("QUESTION_NOT_FOUND"));
        return await IsLockedAsync(questionId, ct) ? Conflict(ApiResponse<string>.Fail("QUESTION_LOCKED")) : null;
    }

    private async Task<bool> IsLockedAsync(int questionId, CancellationToken ct) =>
        await _db.ExamDetailQuestions.AnyAsync(x => x.QuestionId == questionId, ct) ||
        await _db.AnswerSubmissions.AnyAsync(x => x.QuestionId == questionId, ct) ||
        await _db.QuestionAnswers.AnyAsync(x => x.QuestionId == questionId, ct);

    private async Task<object> ViewAsync(int questionId, Guid userId, CancellationToken ct)
    {
        var rows = await _db.QuestionMedia.AsNoTracking().Where(x => x.QuestionId == questionId).OrderBy(x => x.SortOrder)
            .Select(x => new { x.MediaId, x.SortOrder, x.Media.Kind, x.Media.ContentType, x.Media.Width, x.Media.Height, x.Media.SizeBytes, x.Media.OriginalName })
            .ToListAsync(ct);
        return new
        {
            locked = await IsLockedAsync(questionId, ct),
            maxPerQuestion = _settings.Limits.MaxPerQuestion, maxAudioPerQuestion = _settings.Limits.MaxAudioPerQuestion,
            items = rows.Select(x => new
            {
                id = x.MediaId, sortOrder = x.SortOrder, kind = x.Kind, contentType = x.ContentType, width = x.Width, height = x.Height,
                sizeBytes = x.SizeBytes, originalName = x.OriginalName, url = MediaUrls.Url(_settings, x.MediaId, userId)
            }).ToList()
        };
    }
}
