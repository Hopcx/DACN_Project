using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

public sealed record CreatePracticeRequest(int SubjectId, List<int>? ChapterIds, List<int>? QuestionLevelIds,
    List<int>? TagIds, int QuestionCount, string? RevealMode, string? Kind = null, int? DurationMinutes = null);
public sealed record PracticeSaveRequest(int Revision, IReadOnlyList<AttemptAnswerDto>? Answers);
public sealed record PracticeFinishRequest(int Revision);
// To-be T16: Text = bài làm câu điền ngắn (loại 4)
// To-be T17: Items = câu trả lời từng mục (ghép cặp / nhiều mệnh đề)
public sealed record PracticeCheckRequest(int Revision, IReadOnlyList<Guid>? SelectedAnswerIds, string? Text = null,
    IReadOnlyList<AttemptItemDto>? Items = null);

/// <summary>
/// To-be T5: Luyện tập của học viên. Chỉ môn của lớp đã được duyệt (Q18); chỉ chủ phiên thấy phiên (người khác 404).
/// Không dùng ExamAttempts/lịch thi. Mọi thao tác ghi của một học viên tuần tự hóa bằng app lock theo user.
/// RevealMode 'perQuestion' (T6): POST {id}/questions/{qid}/check chốt từng câu và trả đáp án + lời giải của câu đó.
/// Kind 'mock' (T9 — Thi thử): có hạn nộp (DeadlineUtc), chỉ hiện đáp án sau khi nộp; quá hạn + 5 giây thì chốt bằng nháp
/// đã lưu — lười ở mọi request của phiên và bằng worker ExpiredPracticeFinalizerService. Không tính điểm lớp (Q7).
/// </summary>
[ApiController]
[Authorize(Policy = "Student")]
[Route("web/student/practice")]
public class PracticeController : ControllerBase
{
    private const string NotFoundCode = "PRACTICE_NOT_FOUND";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ProjectDACNDbContext _db;
    private readonly PracticeFinalizer _finalizer;
    private readonly MediaSettings _media;
    public PracticeController(ProjectDACNDbContext db, PracticeFinalizer finalizer, MediaSettings media) { _db = db; _finalizer = finalizer; _media = media; }

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static IActionResult Deny(string code) => new ConflictObjectResult(ApiResponse<string>.Fail(code));
    private static AttemptSnapshot Snapshot(PracticeSession row) => JsonSerializer.Deserialize<AttemptSnapshot>(row.SnapshotJson, Json)!;
    private static IReadOnlyList<AttemptAnswerDto> Draft(PracticeSession row) =>
        JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.DraftJson, Json)!;
    private static IReadOnlyList<Guid> Checked(PracticeSession row) => Checked(row.CheckedJson);
    private static IReadOnlyList<Guid> Checked(string checkedJson) =>
        JsonSerializer.Deserialize<List<Guid>>(checkedJson, Json) ?? new List<Guid>();
    // Cột datetime2 đọc ra Kind Unspecified: gắn UTC để JSON có hậu tố Z
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    // Môn được luyện: lớp hoạt động có SubjectId, học viên là thành viên đã duyệt, môn hoạt động.
    private IQueryable<Subject> AllowedSubjects(Guid userId) => _db.Subjects.Where(subject => subject.Status == 1 &&
        _db.Classes.Any(c => c.Status == 1 && c.SubjectId == subject.Id &&
            c.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)));

    private Task LockAsync(Guid userId) => PracticeFinalizer.LockAsync(_db, userId);

    private Task<PracticeSession?> Owned(Guid id, Guid userId) =>
        _db.PracticeSessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    // Đọc phiên của chủ; nếu là Thi thử đã quá hạn thì chốt trước (giao dịch + khóa riêng) rồi trả trạng thái mới.
    private async Task<PracticeSession?> OwnedFinalizedAsync(Guid id, Guid userId)
    {
        var row = await Owned(id, userId);
        if (row == null || row.State != "inProgress" || !PracticeTiming.IsExpired(row.DeadlineUtc, DateTime.UtcNow)) return row;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId);
        await _db.Entry(row).ReloadAsync();
        // Phiên bị bỏ (xóa) giữa lúc đọc và lúc lấy khóa → coi như không còn
        if (_db.Entry(row).State == EntityState.Detached) { await tx.CommitAsync(); return null; }
        await _finalizer.FinalizeIfExpiredAsync(row, DateTime.UtcNow);
        await tx.CommitAsync();
        return row;
    }

    // Câu dùng được theo tiêu chí (lọc ở SQL, kiểm tập đáp án trong bộ nhớ).
    private async Task<List<Question>> PoolAsync(PracticeSnapshotFactory.Criteria criteria)
    {
        var query = _db.Questions.AsNoTracking().Include(x => x.Answers).Include(x => x.QuestionTags)
            .Where(x => x.SubjectId == criteria.SubjectId && x.Status == 1 && x.QuestionTypeId >= 1 && x.QuestionTypeId <= QuestionKinds.Max);
        if (criteria.ChapterIds.Count > 0)
            query = query.Where(x => x.ChapterId != null && criteria.ChapterIds.Contains(x.ChapterId.Value));
        if (criteria.QuestionLevelIds.Count > 0)
            query = query.Where(x => x.QuestionLevelId != null && criteria.QuestionLevelIds.Contains(x.QuestionLevelId.Value));
        if (criteria.TagIds.Count > 0)
            query = query.Where(x => x.QuestionTags.Any(t => criteria.TagIds.Contains(t.TagId)));
        var rows = await query.AsSplitQuery().ToListAsync();
        return rows.Where(x => PracticeSnapshotFactory.IsUsable(x, criteria.SubjectId)).ToList();
    }

    /// <summary>Review T15: chỉ nạp media cho các câu đã chọn (không nạp cho cả pool) trước khi chụp snapshot.
    /// T18: nạp luôn đoạn dùng chung (chữ + media) của các câu con đã chọn.</summary>
    private async Task<bool> AttachMediaAsync(IReadOnlyList<Question> questions)
    {
        var ids = questions.Select(x => x.Id).ToList();
        var media = await _db.QuestionMedia.AsNoTracking().Include(x => x.Media).Where(x => ids.Contains(x.QuestionId)).ToListAsync();
        var byQuestion = media.ToLookup(x => x.QuestionId);
        foreach (var question in questions) question.QuestionMedia = byQuestion[question.Id].ToList();
        var groupIds = questions.Where(x => x.GroupId != null).Select(x => x.GroupId!.Value).Distinct().ToList();
        if (groupIds.Count == 0) return true;
        var groups = await _db.QuestionGroups.AsNoTracking().Include(x => x.Media).ThenInclude(x => x.Media)
            .Where(x => groupIds.Contains(x.Id)).AsSplitQuery().ToDictionaryAsync(x => x.Id);
        // Review T18: đoạn bị xóa giữa lúc chọn câu và lúc chụp → báo 409 cho học viên thử lại (không 500)
        if (groupIds.Any(id => !groups.ContainsKey(id))) return false;
        foreach (var question in questions.Where(x => x.GroupId != null)) question.Group = groups[question.GroupId!.Value];
        return true;
    }

    /// <summary>
    /// T18 (QT18-2/3): chia pool thành đơn vị — câu lẻ, hoặc cả đoạn khi đoạn đang hoạt động và pool có đủ MỌI câu con chưa xóa
    /// (câu con bị ẩn/không dùng được/không khớp bộ lọc → bỏ cả đoạn).
    /// </summary>
    private async Task<List<IReadOnlyList<Question>>> UnitsAsync(IReadOnlyList<Question> pool) =>
        GroupedSelection.Units(pool, x => x.GroupId, x => x.GroupOrder ?? 0, await GroupSizesAsync(pool));

    /// <summary>T18: số câu con chưa xóa của các đoạn đang hoạt động có mặt trong <paramref name="questions"/>.</summary>
    private async Task<Dictionary<int, int>> GroupSizesAsync(IEnumerable<Question> questions)
    {
        var groupIds = questions.Where(x => x.GroupId != null).Select(x => x.GroupId!.Value).Distinct().ToList();
        return groupIds.Count == 0 ? new Dictionary<int, int>()
            : await _db.Questions.AsNoTracking().Where(x => x.GroupId != null && groupIds.Contains(x.GroupId.Value) && x.Status != 255 &&
                    _db.QuestionGroups.Any(g => g.Id == x.GroupId && g.Status == 1))
                .GroupBy(x => x.GroupId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    private static ObjectResult NotEnough(int available, int requested) => new ConflictObjectResult(new ApiResponse<object>
    {
        Success = false, Message = "PRACTICE_NOT_ENOUGH_QUESTIONS", Data = new { available, requested }
    });

    private static int? DurationOf(PracticeSession row)
    {
        var criteria = JsonSerializer.Deserialize<JsonElement>(row.CriteriaJson);
        return criteria.TryGetProperty("durationMinutes", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
    }

    private async Task<object> StateAsync(PracticeSession row)
    {
        var subjectName = await _db.Subjects.Where(x => x.Id == row.SubjectId).Select(x => x.Name).FirstOrDefaultAsync();
        var snapshot = Snapshot(row); var draft = Draft(row);
        return new
        {
            id = row.Id, subjectId = row.SubjectId, subjectName, kind = row.Kind, revealMode = row.RevealMode,
            state = row.State, revision = row.Revision, questionCount = row.QuestionCount, correctCount = row.CorrectCount,
            createdAtUtc = Utc(row.CreatedAtUtc), finishedAtUtc = Utc(row.FinishedAtUtc),
            // To-be T9: đồng hồ Thi thử chạy theo máy chủ
            deadlineUtc = Utc(row.DeadlineUtc), serverNow = DateTime.UtcNow, durationMinutes = DurationOf(row),
            criteria = JsonSerializer.Deserialize<JsonElement>(row.CriteriaJson),
            // Không có đáp án đúng, điểm hay lời giải trong DTO làm bài
            questions = AttemptSnapshotFactory.ForStudent(snapshot, MediaUrlFactory.For(_media, row.UserId)), answers = draft,
            // To-be T18: đoạn dùng chung (chữ + media URL ký cho chủ phiên)
            groups = AttemptSnapshotFactory.GroupsForStudent(snapshot, MediaUrlFactory.For(_media, row.UserId)),
            // To-be T6: chỉ câu đã chốt mới có đáp án đúng + lời giải
            @checked = PracticeCheck.Results(snapshot, draft, Checked(row))
        };
    }

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var subjects = await AllowedSubjects(userId.Value).AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var subjectIds = subjects.Select(x => x.Id).ToList();
        var questions = (await _db.Questions.AsNoTracking().Include(x => x.Answers).Include(x => x.QuestionTags)
                .Where(x => subjectIds.Contains(x.SubjectId) && x.Status == 1 && x.QuestionTypeId >= 1 && x.QuestionTypeId <= QuestionKinds.Max)
                .AsSplitQuery().ToListAsync())
            .Where(x => PracticeSnapshotFactory.IsUsable(x, x.SubjectId)).ToList();
        var chapters = await _db.Chapters.AsNoTracking().Where(x => subjectIds.Contains(x.SubjectId) && x.Status == 1)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.SubjectId, x.Name }).ToListAsync();
        var levels = await _db.QuestionLevels.AsNoTracking().Where(x => x.Status == true).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var tagIds = questions.SelectMany(x => x.QuestionTags.Select(t => t.TagId)).Distinct().ToList();
        var tags = await _db.Tags.AsNoTracking().Where(x => tagIds.Contains(x.Id) && x.Status == 1).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        // Review T18: đếm theo đơn vị (đoạn chỉ tính khi đủ câu con khớp bộ lọc) để khớp đúng số câu tạo phiên được
        var sizes = await GroupSizesAsync(questions);
        int Usable(IEnumerable<Question> filtered) => GroupedSelection.Units(filtered, x => x.GroupId, x => x.GroupOrder ?? 0, sizes).Sum(x => x.Count);
        var result = subjects.Select(subject =>
        {
            var pool = questions.Where(x => x.SubjectId == subject.Id).ToList();
            return new
            {
                id = subject.Id, name = subject.Name, questionCount = Usable(pool),
                // có đoạn dùng chung dùng được → FE báo trước "có thể nhiều hơn N câu"
                hasGroups = GroupedSelection.Units(pool, x => x.GroupId, x => x.GroupOrder ?? 0, sizes).Any(u => u.Count > 1),
                chapters = chapters.Where(x => x.SubjectId == subject.Id)
                    .Select(x => new { id = x.Id, name = x.Name, questionCount = Usable(pool.Where(q => q.ChapterId == x.Id)) }).ToList(),
                levels = levels.Select(x => new { id = x.Id, name = x.Name, questionCount = Usable(pool.Where(q => q.QuestionLevelId == x.Id)) })
                    .Where(x => x.questionCount > 0).ToList(),
                tags = tags.Select(x => new { id = x.Id, name = x.Name, questionCount = Usable(pool.Where(q => q.QuestionTags.Any(t => t.TagId == x.Id))) })
                    .Where(x => x.questionCount > 0).ToList()
            };
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            minQuestions = PracticeSnapshotFactory.MinQuestions, maxQuestions = PracticeSnapshotFactory.MaxQuestions,
            maxOpenSessions = PracticeSnapshotFactory.MaxOpenSessions, maxOpenMocks = PracticeSnapshotFactory.MaxOpenMocks,
            minDurationMinutes = PracticeSnapshotFactory.MinDurationMinutes, maxDurationMinutes = PracticeSnapshotFactory.MaxDurationMinutes,
            subjects = result
        }));
    }

    // Giới hạn phiên đang làm theo loại: 3 luyện tập (QT5-2 a), 1 thi thử (QT9-3 a)
    private async Task<IActionResult?> OpenLimitAsync(Guid userId, string kind)
    {
        var open = await _db.PracticeSessions.CountAsync(x => x.UserId == userId && x.State == "inProgress" && x.Kind == kind);
        return kind == PracticeSnapshotFactory.KindMock
            ? open >= PracticeSnapshotFactory.MaxOpenMocks ? Deny("MOCK_ALREADY_OPEN") : null
            : open >= PracticeSnapshotFactory.MaxOpenSessions ? Deny("PRACTICE_TOO_MANY_OPEN") : null;
    }

    // Trước khi đếm phiên mở: chốt các Thi thử đã quá hạn của chính học viên (không để phiên hết giờ chặn tạo mới)
    private async Task FinalizeExpiredOfUserAsync(Guid userId)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-PracticeTiming.GraceSeconds);
        var rows = await _db.PracticeSessions.Where(x => x.UserId == userId && x.State == "inProgress" &&
            x.DeadlineUtc != null && x.DeadlineUtc <= cutoff).ToListAsync();
        foreach (var row in rows) await _finalizer.FinalizeIfExpiredAsync(row, DateTime.UtcNow);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePracticeRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var (criteria, error) = PracticeSnapshotFactory.Normalize(request.SubjectId, request.ChapterIds,
            request.QuestionLevelIds, request.TagIds, request.QuestionCount, request.RevealMode, request.Kind, request.DurationMinutes);
        if (criteria == null) return BadRequest(ApiResponse<string>.Fail(error!));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        if (!await AllowedSubjects(userId.Value).AnyAsync(x => x.Id == criteria.SubjectId))
            return StatusCode(403, ApiResponse<string>.Fail("PRACTICE_SUBJECT_FORBIDDEN"));
        // Bộ lọc khớp options: chương của môn đang hoạt động, mức độ đang dùng, tag đang hoạt động
        if (criteria.ChapterIds.Count > 0 && await _db.Chapters.CountAsync(x => criteria.ChapterIds.Contains(x.Id) &&
                x.SubjectId == criteria.SubjectId && x.Status == 1) != criteria.ChapterIds.Count ||
            criteria.QuestionLevelIds.Count > 0 && await _db.QuestionLevels.CountAsync(x =>
                criteria.QuestionLevelIds.Contains(x.Id) && x.Status == true) != criteria.QuestionLevelIds.Count ||
            criteria.TagIds.Count > 0 && await _db.Tags.CountAsync(x => criteria.TagIds.Contains(x.Id) && x.Status == 1) != criteria.TagIds.Count)
            return BadRequest(ApiResponse<string>.Fail("Chương, mức độ hoặc tag không tồn tại, đã ẩn hoặc không thuộc môn."));
        await FinalizeExpiredOfUserAsync(userId.Value);
        if (await OpenLimitAsync(userId.Value, criteria.Kind) is { } limited) { await tx.CommitAsync(); return limited; }
        var pool = await PoolAsync(criteria);
        // T18 (QT18-3): chọn theo đơn vị (câu lẻ / cả đoạn); được vượt nhẹ để không cắt đoạn (≤ MaxPracticeQuestions)
        var units = await UnitsAsync(pool);
        var chosen = GroupedSelection.PickAtLeast(units, criteria.QuestionCount, Random.Shared);
        if (chosen == null) { await tx.CommitAsync(); return NotEnough(units.Sum(x => x.Count), criteria.QuestionCount); }
        var picked = chosen.SelectMany(x => x).ToList();
        if (!await AttachMediaAsync(picked)) { await tx.CommitAsync(); return Conflict(ApiResponse<string>.Fail("QUESTION_GROUPS_CHANGED")); }
        var row = NewSession(userId.Value, criteria.SubjectId, criteria.RevealMode, criteria.Kind, criteria.DurationMinutes, criteria, null, picked);
        _db.PracticeSessions.Add(row);
        await MediaReferenceWriter.AddFromJsonAsync(_db, "practice", row.Id, row.SnapshotJson);   // To-be T15
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return StatusCode(201, ApiResponse<object>.Ok(await StateAsync(row)));
    }

    private static PracticeSession NewSession(Guid userId, int subjectId, string revealMode, string kind, int? durationMinutes,
        PracticeSnapshotFactory.Criteria? criteria, Guid? retryOf, IReadOnlyList<Question> questions)
    {
        var now = DateTime.UtcNow;
        return new PracticeSession
        {
            Id = Guid.NewGuid(), UserId = userId, SubjectId = subjectId, Kind = kind, RevealMode = revealMode,
            State = "inProgress", Revision = 0, QuestionCount = questions.Count, CreatedAtUtc = now,
            DeadlineUtc = durationMinutes.HasValue ? PracticeTiming.Deadline(now, durationMinutes.Value) : null,
            CriteriaJson = JsonSerializer.Serialize(new
            {
                subjectId, chapterIds = criteria?.ChapterIds ?? Array.Empty<int>(),
                questionLevelIds = criteria?.QuestionLevelIds ?? Array.Empty<int>(), tagIds = criteria?.TagIds ?? Array.Empty<int>(),
                questionCount = questions.Count, revealMode, kind, durationMinutes, retryOf
            }, Json),
            SnapshotJson = JsonSerializer.Serialize(PracticeSnapshotFactory.Create(subjectId, questions), Json),
            DraftJson = "[]"
        };
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 50) return BadRequest(ApiResponse<string>.Fail("Trang hoặc số dòng không hợp lệ."));
        var query = _db.PracticeSessions.AsNoTracking().Where(x => x.UserId == userId);
        var totalCount = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.SubjectId, SubjectName = _db.Subjects.Where(s => s.Id == x.SubjectId).Select(s => s.Name).FirstOrDefault(),
                x.Kind, x.RevealMode, x.State, x.QuestionCount, x.CorrectCount, x.CreatedAtUtc, x.FinishedAtUtc, x.DeadlineUtc, x.CheckedJson
            }).ToListAsync();
        var now = DateTime.UtcNow;
        var items = rows.Select(x =>
        {
            // Thi thử quá hạn chưa kịp chốt: hiện là đã hết giờ (mở phiên sẽ chốt)
            var expired = x.State == "inProgress" && PracticeTiming.IsExpired(x.DeadlineUtc, now);
            // Nợ sau T9: thời gian làm bài (đã nộp). T19 (nợ L5): hết giờ chưa chốt = đúng thời lượng được cấp (hạn − lúc tạo)
            var end = x.FinishedAtUtc ?? (expired ? x.DeadlineUtc : null);
            return new
            {
                id = x.Id, subjectId = x.SubjectId, subjectName = x.SubjectName, kind = x.Kind, revealMode = x.RevealMode,
                state = expired ? "expired" : x.State,
                questionCount = x.QuestionCount, correctCount = x.CorrectCount,
                createdAtUtc = Utc(x.CreatedAtUtc), finishedAtUtc = Utc(x.FinishedAtUtc), deadlineUtc = Utc(x.DeadlineUtc),
                durationSeconds = end.HasValue ? (long?)Math.Max(0, Math.Round((end.Value - x.CreatedAtUtc).TotalSeconds)) : null,
                // Số câu đã kiểm tra (FE ẩn "Bỏ phiên" khi > 0)
                checkedCount = Checked(x.CheckedJson).Count
            };
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new { page, pageSize, totalCount, items }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var row = await OwnedFinalizedAsync(id, userId.Value);
        return row == null ? NotFound(ApiResponse<string>.Fail(NotFoundCode)) : Ok(ApiResponse<object>.Ok(await StateAsync(row)));
    }

    [HttpPut("{id:guid}/answers")]
    public async Task<IActionResult> Save(Guid id, [FromBody] PracticeSaveRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        // T9: Thi thử quá hạn + ân hạn → chốt bằng nháp đã lưu, không nhận bản lưu muộn
        var now = DateTime.UtcNow;
        if (await _finalizer.FinalizeIfExpiredAsync(row, now)) { await tx.CommitAsync(); return Deny("PRACTICE_EXPIRED"); }
        // Đã được worker/request khác chốt vì hết giờ → vẫn báo hết giờ (không phải "đã nộp")
        if (row.State != "inProgress")
            return Deny(row.Kind == PracticeSnapshotFactory.KindMock && PracticeTiming.IsExpired(row.DeadlineUtc, now) ? "PRACTICE_EXPIRED" : "PRACTICE_CLOSED");
        if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
        IReadOnlyList<AttemptAnswerDto> draft;
        // T6: câu đã chốt giữ lựa chọn; payload đổi câu đã chốt → 409
        try { draft = PracticeCheck.Merge(Snapshot(row), Draft(row), Checked(row), request.Answers); }
        catch (PracticeQuestionLockedException) { return Deny("PRACTICE_QUESTION_LOCKED"); }
        catch (ArgumentException) { return BadRequest(ApiResponse<string>.Fail("INVALID_SELECTION")); }
        row.DraftJson = JsonSerializer.Serialize(draft, Json);
        row.Revision++;
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { id = row.Id, revision = row.Revision, answers = draft }));
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> Finish(Guid id, [FromBody] PracticeFinishRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        var now = DateTime.UtcNow;
        await _finalizer.FinalizeIfExpiredAsync(row, now);
        // Idempotent: phiên đã chốt (nộp, hoặc hết giờ) trả lại đúng kết quả cũ, không kiểm revision
        if (row.State == "inProgress")
        {
            if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
            await PracticeFinalizer.ScoreAsync(_db, row, PracticeTiming.FinishedAt(row.DeadlineUtc, now));
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            id = row.Id, state = row.State, revision = row.Revision, questionCount = row.QuestionCount,
            correctCount = row.CorrectCount, finishedAtUtc = Utc(row.FinishedAtUtc)
        }));
    }

    // To-be T6: "Kiểm tra câu này" (chỉ phiên perQuestion đang làm). Lưu lựa chọn gửi kèm và chốt câu trong cùng
    // giao dịch; trả đúng/sai, đáp án đúng, lời giải của riêng câu đó. Câu đã chốt → trả lại kết quả cũ (idempotent).
    // Thi thử luôn revealMode 'end' nên bị chặn ở đây.
    [HttpPost("{id:guid}/questions/{questionId:guid}/check")]
    public async Task<IActionResult> Check(Guid id, Guid questionId, [FromBody] PracticeCheckRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        if (row.RevealMode != "perQuestion") return Deny("PRACTICE_CHECK_NOT_ALLOWED");
        var snapshot = Snapshot(row);
        var question = snapshot.Questions.FirstOrDefault(x => x.Id == questionId);
        if (question == null) return NotFound(ApiResponse<string>.Fail("PRACTICE_QUESTION_NOT_FOUND"));
        var checkedIds = Checked(row);
        if (checkedIds.Contains(questionId))
        {
            var previous = Draft(row).FirstOrDefault(x => x.QuestionId == questionId) ?? new AttemptAnswerDto(questionId, Array.Empty<Guid>());
            await tx.CommitAsync();
            return Ok(ApiResponse<object>.Ok(new { result = PracticeCheck.Result(question, previous), revision = row.Revision }));
        }
        if (row.State != "inProgress") return Deny("PRACTICE_CLOSED");
        if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
        IReadOnlyList<AttemptAnswerDto> draft; IReadOnlyList<Guid> nextChecked;
        try { (draft, nextChecked) = PracticeCheck.Apply(snapshot, Draft(row), checkedIds, questionId, request.SelectedAnswerIds, request.Text, request.Items); }
        catch (ArgumentException) { return BadRequest(ApiResponse<string>.Fail("INVALID_SELECTION")); }
        row.DraftJson = JsonSerializer.Serialize(draft, Json);
        row.CheckedJson = JsonSerializer.Serialize(nextChecked, Json);
        row.Revision++;
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        var answered = draft.First(x => x.QuestionId == questionId);
        return Ok(ApiResponse<object>.Ok(new { result = PracticeCheck.Result(question, answered), revision = row.Revision }));
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var row = await OwnedFinalizedAsync(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        if (row.State != "finished") return Deny("PRACTICE_NOT_FINISHED");
        var byQuestion = Draft(row).ToDictionary(x => x.QuestionId);
        var reviewSnapshot = Snapshot(row);
        var questions = reviewSnapshot.Questions.Select(question =>
        {
            var answer = byQuestion.GetValueOrDefault(question.Id) ?? new AttemptAnswerDto(question.Id, Array.Empty<Guid>());
            var shortAnswer = AnswerGrading.IsShortAnswer(question);
            return new
            {
                id = question.Id, questionTypeId = question.QuestionTypeId, content = question.Content,
                groupId = question.GroupId,   // To-be T18
                // To-be T16: cùng hàm chấm với điểm; câu điền ngắn trả bài làm + đáp án chấp nhận, không có lựa chọn
                selectedAnswerIds = answer.SelectedAnswerIds, isCorrect = AnswerGrading.IsCorrect(question, answer),
                text = shortAnswer ? answer.Text : null,
                acceptedAnswers = shortAnswer ? AnswerGrading.AcceptedAnswers(question) : null,
                // To-be T17: câu trả lời từng mục + đáp án đúng từng mục + vế phải theo thứ tự đã thấy
                items = AnswerGrading.HasItems(question) ? answer.Items ?? [] : null,
                correctItems = AnswerGrading.CorrectItems(question),
                matches = AttemptSnapshotFactory.MatchesFor(question),
                options = question.Options.Where(_ => !shortAnswer).Select(option => new { id = option.Id, content = option.Content, isCorrect = option.IsCorrect,
                    matchId = option.MatchId, matchContent = option.MatchContent }).ToList(),
                explanation = question.Explanation,
                media = AttemptSnapshotFactory.MediaFor(question, MediaUrlFactory.For(_media, row.UserId))   // To-be T15
            };
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            id = row.Id, state = row.State, kind = row.Kind, revealMode = row.RevealMode, questionCount = row.QuestionCount,
            correctCount = row.CorrectCount, finishedAtUtc = Utc(row.FinishedAtUtc), questions,
            groups = AttemptSnapshotFactory.GroupsForStudent(reviewSnapshot, MediaUrlFactory.For(_media, row.UserId))   // To-be T18
        }));
    }

    // "Làm lại bộ này" (Q4, QT5-3 a): cùng câu gốc, chụp lại từ ngân hàng hiện tại, bỏ câu không còn dùng được, xáo lại.
    // Thi thử làm lại tạo Thi thử mới cùng thời lượng (QT9-3 a).
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var source = await Owned(id, userId.Value);
        if (source == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        await _finalizer.FinalizeIfExpiredAsync(source, DateTime.UtcNow);
        // QT6-4 a: chỉ làm lại phiên đã nộp (không xem đáp án từng câu rồi làm lại bộ đó khi chưa nộp)
        if (source.State != "finished") { await tx.CommitAsync(); return Deny("PRACTICE_NOT_FINISHED"); }
        if (!await AllowedSubjects(userId.Value).AnyAsync(x => x.Id == source.SubjectId))
        { await tx.CommitAsync(); return StatusCode(403, ApiResponse<string>.Fail("PRACTICE_SUBJECT_FORBIDDEN")); }
        await FinalizeExpiredOfUserAsync(userId.Value);
        if (await OpenLimitAsync(userId.Value, source.Kind) is { } limited) { await tx.CommitAsync(); return limited; }
        var sourceIds = Snapshot(source).Questions.Select(x => x.SourceQuestionId).Distinct().ToList();
        var pool = (await _db.Questions.AsNoTracking().Include(x => x.Answers).Include(x => x.QuestionTags).Include(x => x.QuestionMedia).ThenInclude(x => x.Media).Where(x => sourceIds.Contains(x.Id))
                .AsSplitQuery().ToListAsync())
            .Where(x => PracticeSnapshotFactory.IsUsable(x, source.SubjectId)).ToList();
        // T18: đoạn chỉ giữ khi còn đủ câu con hợp lệ; xáo theo đơn vị (đoạn liền khối)
        var retryUnits = await UnitsAsync(pool);
        if (retryUnits.Count == 0) { await tx.CommitAsync(); return NotEnough(0, sourceIds.Count); }
        var picked = GroupedSelection.PickExact(retryUnits, retryUnits.Sum(x => x.Count), Random.Shared)!.SelectMany(x => x).ToList();
        if (!await AttachMediaAsync(picked)) { await tx.CommitAsync(); return Conflict(ApiResponse<string>.Fail("QUESTION_GROUPS_CHANGED")); }
        var row = NewSession(userId.Value, source.SubjectId, source.RevealMode, source.Kind,
            source.Kind == PracticeSnapshotFactory.KindMock ? DurationOf(source) ?? PracticeTiming.SuggestedMinutes(picked.Count) : null,
            null, source.Id, picked);
        _db.PracticeSessions.Add(row);
        await MediaReferenceWriter.AddFromJsonAsync(_db, "practice", row.Id, row.SnapshotJson);   // To-be T15
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return StatusCode(201, ApiResponse<object>.Ok(await StateAsync(row)));
    }

    // Bỏ phiên đang làm (QT5-2 a): xóa hẳn; phiên đã chốt giữ làm lịch sử.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Abandon(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        // T9: Thi thử đã hết giờ được chốt (giữ làm lịch sử) thay vì xóa
        if (await _finalizer.FinalizeIfExpiredAsync(row, DateTime.UtcNow)) { await tx.CommitAsync(); return Deny("PRACTICE_FINISHED"); }
        if (row.State != "inProgress") return Deny("PRACTICE_FINISHED");
        // QT6-4 a: đã xem đáp án ít nhất một câu thì không bỏ được, chỉ còn nộp
        if (Checked(row).Count > 0) return Deny("PRACTICE_HAS_CHECKED");
        _db.PracticeSessions.Remove(row);
        await _db.SaveChangesAsync();
        // To-be T8 (QT8-5 a): xóa luôn đánh dấu câu của phiên bị bỏ, cùng giao dịch
        await _db.ReviewFlags.Where(x => x.UserId == userId.Value && x.SourceKind == ReviewFlagRules.Practice && x.SourceId == id)
            .ExecuteDeleteAsync();
        // To-be T15: phiên bị bỏ không còn tham chiếu media (để media có thể thành mồ côi)
        await _db.MediaReferences.Where(x => x.SourceKind == "practice" && x.SourceId == id).ExecuteDeleteAsync();
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
