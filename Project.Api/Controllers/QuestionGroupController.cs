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
/// To-be T18 (QT18-1/2): đoạn dùng chung — chữ + media, 2–10 câu con cùng môn (mỗi câu thuộc tối đa một đoạn, có thứ tự).
/// Khóa sửa/xóa/media khi có câu con đã vào đề hoặc đã có bài làm (như khóa sửa câu hỏi). Ẩn/hiện luôn được (ẩn = không chọn mới).
/// Mọi thay đổi ghi audit.
/// </summary>
[ApiController]
[Authorize(Policy = "QuestionManagement")]
[DeadlockAsConflict]   // Review T18
[Route("web/question-groups")]
public sealed class QuestionGroupController : ControllerBase
{
    public const int MinQuestions = 2, MaxQuestions = 10, MaxTitle = 200, MaxContent = 8000;
    private readonly ProjectDACNDbContext _db;
    private readonly MediaSettings _settings;
    private readonly AuditWriter _audit;

    public QuestionGroupController(ProjectDACNDbContext db, MediaSettings settings, AuditWriter audit)
    { _db = db; _settings = settings; _audit = audit; }

    public sealed record SaveRequest(int SubjectId, string? Title, string? Content, List<int>? QuestionIds);
    public sealed record StatusRequest(byte Status);
    public sealed record AttachRequest(Guid MediaId);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? subjectId, [FromQuery] bool includeHidden = true, CancellationToken ct = default)
    {
        var query = _db.QuestionGroups.AsNoTracking();
        if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId.Value);
        if (!includeHidden) query = query.Where(x => x.Status == 1);
        var rows = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                id = x.Id, subjectId = x.SubjectId, subjectName = x.Subject.Name, title = x.Title, status = x.Status,
                questionCount = x.Questions.Count(q => q.Status != 255), mediaCount = x.Media.Count,
                locked = x.Questions.Any(q => _db.ExamDetailQuestions.Any(d => d.QuestionId == q.Id) ||
                    _db.AnswerSubmissions.Any(a => a.QuestionId == q.Id) || _db.QuestionAnswers.Any(a => a.QuestionId == q.Id)),
                updatedAt = x.UpdatedAt ?? x.CreatedAt
            }).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(rows));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        var view = await ViewAsync(id, userId, ct);
        return view == null ? NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND")) : Ok(ApiResponse<object>.Ok(view));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (Validate(request) is { } bad) return bad;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await MembersProblemAsync(request, null, ct) is { } denied) return denied;
        var now = DateTime.UtcNow;
        var group = new QuestionGroup { SubjectId = request.SubjectId, Title = request.Title!.Trim(), Content = request.Content!.Trim(),
            Status = 1, CreatedBy = userId, CreatedAt = now };
        _db.QuestionGroups.Add(group);
        await _db.SaveChangesAsync(ct);
        await AssignAsync(group.Id, request.QuestionIds!, ct);
        _audit.Add(User, "question.group.create", "QuestionGroup", group.Id.ToString(), $"{group.Title}; câu: {string.Join(",", request.QuestionIds!)}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, ApiResponse<object>.Ok((await ViewAsync(group.Id, userId, ct))!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (Validate(request) is { } bad) return bad;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var group = await LockGroupAsync(id, ct);
        if (group == null) return NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND"));
        if (await IsLockedAsync(id, ct)) return Conflict(ApiResponse<string>.Fail("GROUP_LOCKED"));
        if (await MembersProblemAsync(request, id, ct) is { } denied) return denied;
        group.SubjectId = request.SubjectId; group.Title = request.Title!.Trim(); group.Content = request.Content!.Trim();
        group.UpdatedBy = userId; group.UpdatedAt = DateTime.UtcNow;
        await AssignAsync(id, request.QuestionIds!, ct);
        _audit.Add(User, "question.group.update", "QuestionGroup", id.ToString(), $"{group.Title}; câu: {string.Join(",", request.QuestionIds!)}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok((await ViewAsync(id, userId, ct))!));
    }

    // Ẩn (0) / hiện (1): không đổi đề/lượt đã có; đoạn ẩn không được chọn vào mã đề/luyện tập mới
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (request.Status is not (0 or 1)) return BadRequest(ApiResponse<string>.Fail("Trạng thái chỉ nhận 0 hoặc 1."));
        var group = await _db.QuestionGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (group == null) return NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND"));
        group.Status = request.Status; group.UpdatedBy = userId; group.UpdatedAt = DateTime.UtcNow;
        _audit.Add(User, request.Status == 1 ? "question.group.show" : "question.group.hide", "QuestionGroup", id.ToString(), group.Title);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok((await ViewAsync(id, userId, ct))!));
    }

    // Xóa đoạn: gỡ câu con (thành câu lẻ) và media của đoạn; bị khóa khi câu con đã vào đề/có bài làm
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!TryUser(out _)) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var group = await LockGroupAsync(id, ct);
        if (group == null) return NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND"));
        if (await IsLockedAsync(id, ct)) return Conflict(ApiResponse<string>.Fail("GROUP_LOCKED"));
        await AssignAsync(id, [], ct);
        _db.QuestionGroupMedia.RemoveRange(_db.QuestionGroupMedia.Where(x => x.GroupId == id));
        _db.QuestionGroups.Remove(group);
        _audit.Add(User, "question.group.delete", "QuestionGroup", id.ToString(), group.Title);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<string>.Ok("Đã xóa đoạn dùng chung"));
    }

    [HttpPost("{id:int}/media")]
    public async Task<IActionResult> AttachMedia(int id, [FromBody] AttachRequest request, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var group = await LockGroupAsync(id, ct);
        if (group == null) return NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND"));
        if (await IsLockedAsync(id, ct)) return Conflict(ApiResponse<string>.Fail("GROUP_LOCKED"));
        var media = await _db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.MediaId && x.Status == 1, ct);
        if (media == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_FOUND"));
        var current = await _db.QuestionGroupMedia.Where(x => x.GroupId == id)
            .Select(x => new { x.MediaId, x.SortOrder, x.Media.Kind }).ToListAsync(ct);
        if (current.Any(x => x.MediaId == media.Id)) return Conflict(ApiResponse<string>.Fail("MEDIA_ALREADY_ATTACHED"));
        var limits = _settings.Limits;
        if (current.Count >= limits.MaxPerQuestion) return Conflict(ApiResponse<string>.Fail("MEDIA_LIMIT_REACHED"));
        if (media.Kind == "audio" && current.Count(x => x.Kind == "audio") >= limits.MaxAudioPerQuestion)
            return Conflict(ApiResponse<string>.Fail("MEDIA_AUDIO_LIMIT_REACHED"));
        _db.QuestionGroupMedia.Add(new QuestionGroupMedia { GroupId = id, MediaId = media.Id, SortOrder = current.Count == 0 ? 1 : current.Max(x => x.SortOrder) + 1 });
        _audit.Add(User, "question.group.media.attach", "QuestionGroup", id.ToString(), $"Gắn {media.Kind} {media.Id} ({media.SizeBytes} byte)");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok((await ViewAsync(id, userId, ct))!));
    }

    [HttpDelete("{id:int}/media/{mediaId:guid}")]
    public async Task<IActionResult> DetachMedia(int id, Guid mediaId, CancellationToken ct)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var group = await LockGroupAsync(id, ct);
        if (group == null) return NotFound(ApiResponse<string>.Fail("GROUP_NOT_FOUND"));
        if (await IsLockedAsync(id, ct)) return Conflict(ApiResponse<string>.Fail("GROUP_LOCKED"));
        var link = await _db.QuestionGroupMedia.FirstOrDefaultAsync(x => x.GroupId == id && x.MediaId == mediaId, ct);
        if (link == null) return NotFound(ApiResponse<string>.Fail("MEDIA_NOT_ATTACHED"));
        _db.QuestionGroupMedia.Remove(link);
        _audit.Add(User, "question.group.media.detach", "QuestionGroup", id.ToString(), $"Gỡ media {mediaId}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok((await ViewAsync(id, userId, ct))!));
    }

    private bool TryUser(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static ObjectResult? Validate(SaveRequest request)
    {
        static BadRequestObjectResult Bad(string message) => new(ApiResponse<string>.Fail(message));
        if (request.SubjectId <= 0) return Bad("Môn học không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitle)
            return Bad($"Tiêu đề đoạn cần 1–{MaxTitle} ký tự.");
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > MaxContent)
            return Bad($"Nội dung đoạn cần 1–{MaxContent} ký tự.");
        if (request.QuestionIds == null || request.QuestionIds.Count is < MinQuestions or > MaxQuestions ||
            request.QuestionIds.Any(x => x <= 0) || request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
            return Bad($"Đoạn cần {MinQuestions}–{MaxQuestions} câu hỏi không trùng.");
        return null;
    }

    // Câu con: tồn tại (chưa xóa), cùng môn, chưa thuộc đoạn khác, chưa vào đề/chưa có bài làm; môn hoạt động
    private async Task<ObjectResult?> MembersProblemAsync(SaveRequest request, int? groupId, CancellationToken ct)
    {
        if (!await _db.Subjects.AnyAsync(x => x.Id == request.SubjectId && x.Status == 1, ct))
            return BadRequest(ApiResponse<string>.Fail("Môn học không tồn tại hoặc đã ngừng dùng."));
        var ids = request.QuestionIds!;
        var rows = await _db.Questions.Where(x => ids.Contains(x.Id) && x.Status != 255)
            .Select(x => new { x.Id, x.SubjectId, x.GroupId }).ToListAsync(ct);
        if (rows.Count != ids.Count || rows.Any(x => x.SubjectId != request.SubjectId))
            return BadRequest(ApiResponse<string>.Fail("Câu hỏi không tồn tại, đã xóa hoặc khác môn của đoạn."));
        if (rows.Any(x => x.GroupId != null && x.GroupId != groupId))
            return Conflict(ApiResponse<string>.Fail("QUESTION_IN_OTHER_GROUP"));
        if (await _db.ExamDetailQuestions.AnyAsync(x => ids.Contains(x.QuestionId), ct) ||
            await _db.AnswerSubmissions.AnyAsync(x => ids.Contains(x.QuestionId), ct) ||
            await _db.QuestionAnswers.AnyAsync(x => ids.Contains(x.QuestionId), ct))
            return Conflict(ApiResponse<string>.Fail("QUESTION_LOCKED"));
        return null;
    }

    // Thay toàn bộ thành viên: câu cũ không còn trong danh sách → câu lẻ; câu mới theo thứ tự truyền vào
    private async Task AssignAsync(int groupId, IReadOnlyList<int> questionIds, CancellationToken ct)
    {
        var current = await _db.Questions.Where(x => x.GroupId == groupId || questionIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var question in current)
        {
            var index = questionIds.ToList().IndexOf(question.Id);
            if (index < 0) { question.GroupId = null; question.GroupOrder = null; }
            else { question.GroupId = groupId; question.GroupOrder = index + 1; }
        }
    }

    private async Task<QuestionGroup?> LockGroupAsync(int id, CancellationToken ct) =>
        await _db.QuestionGroups.FromSqlInterpolated($"SELECT * FROM dbo.QuestionGroups WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .FirstOrDefaultAsync(ct);

    private async Task<bool> IsLockedAsync(int groupId, CancellationToken ct)
    {
        var ids = _db.Questions.Where(x => x.GroupId == groupId).Select(x => x.Id);
        return await _db.ExamDetailQuestions.AnyAsync(x => ids.Contains(x.QuestionId), ct) ||
            await _db.AnswerSubmissions.AnyAsync(x => ids.Contains(x.QuestionId), ct) ||
            await _db.QuestionAnswers.AnyAsync(x => ids.Contains(x.QuestionId), ct);
    }

    private async Task<object?> ViewAsync(int id, Guid userId, CancellationToken ct)
    {
        var group = await _db.QuestionGroups.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.SubjectId, SubjectName = x.Subject.Name, x.Title, x.Content, x.Status, x.CreatedAt, x.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (group == null) return null;
        var questions = await _db.Questions.AsNoTracking().Where(x => x.GroupId == id && x.Status != 255).OrderBy(x => x.GroupOrder)
            .Select(x => new { id = x.Id, content = x.Content, questionTypeId = x.QuestionTypeId, status = x.Status, order = x.GroupOrder })
            .ToListAsync(ct);
        var media = await _db.QuestionGroupMedia.AsNoTracking().Where(x => x.GroupId == id).OrderBy(x => x.SortOrder)
            .Select(x => new { x.MediaId, x.SortOrder, x.Media.Kind, x.Media.ContentType, x.Media.Width, x.Media.Height, x.Media.SizeBytes, x.Media.OriginalName })
            .ToListAsync(ct);
        return new
        {
            id = group.Id, subjectId = group.SubjectId, subjectName = group.SubjectName, title = group.Title, content = group.Content,
            status = group.Status, createdAt = group.CreatedAt, updatedAt = group.UpdatedAt, locked = await IsLockedAsync(id, ct),
            questions,
            maxPerQuestion = _settings.Limits.MaxPerQuestion, maxAudioPerQuestion = _settings.Limits.MaxAudioPerQuestion,
            media = media.Select(x => new
            {
                id = x.MediaId, sortOrder = x.SortOrder, kind = x.Kind, contentType = x.ContentType, width = x.Width, height = x.Height,
                sizeBytes = x.SizeBytes, originalName = x.OriginalName, url = MediaUrls.Url(_settings, x.MediaId, userId)
            }).ToList()
        };
    }
}
