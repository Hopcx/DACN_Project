using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

public sealed record CreatePracticeRequest(int SubjectId, List<int>? ChapterIds, List<int>? QuestionLevelIds,
    List<int>? TagIds, int QuestionCount, string? RevealMode);
public sealed record PracticeSaveRequest(int Revision, IReadOnlyList<AttemptAnswerDto>? Answers);
public sealed record PracticeFinishRequest(int Revision);

/// <summary>
/// To-be T5: Luyện tập của học viên. Chỉ môn của lớp đã được duyệt (Q18); chỉ chủ phiên thấy phiên (người khác 404).
/// Không dùng ExamAttempts/lịch thi/worker. Mọi thao tác ghi của một học viên tuần tự hóa bằng app lock theo user.
/// RevealMode 'perQuestion' được lưu nhưng tới T6 vẫn chỉ mở đáp án sau finish (QT5-4 a).
/// </summary>
[ApiController]
[Authorize(Policy = "Student")]
[Route("web/student/practice")]
public class PracticeController : ControllerBase
{
    private const string NotFoundCode = "PRACTICE_NOT_FOUND";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ProjectDACNDbContext _db;
    public PracticeController(ProjectDACNDbContext db) => _db = db;

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static IActionResult Deny(string code) => new ConflictObjectResult(ApiResponse<string>.Fail(code));
    private static AttemptSnapshot Snapshot(PracticeSession row) => JsonSerializer.Deserialize<AttemptSnapshot>(row.SnapshotJson, Json)!;
    private static IReadOnlyList<AttemptAnswerDto> Draft(PracticeSession row) =>
        JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.DraftJson, Json)!;

    // Môn được luyện: lớp hoạt động có SubjectId, học viên là thành viên đã duyệt, môn hoạt động.
    private IQueryable<Subject> AllowedSubjects(Guid userId) => _db.Subjects.Where(subject => subject.Status == 1 &&
        _db.Classes.Any(c => c.Status == 1 && c.SubjectId == subject.Id &&
            c.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)));

    private async Task LockAsync(Guid userId)
    {
        if (_db.Database.CurrentTransaction == null) throw new InvalidOperationException("Practice lock requires a transaction.");
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {"DACN:practice:" + userId.ToString("N")},
                @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51010, 'Unable to acquire practice lock', 1;
            """);
    }

    private Task<PracticeSession?> Owned(Guid id, Guid userId) =>
        _db.PracticeSessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    // Câu dùng được theo tiêu chí (lọc ở SQL, kiểm tập đáp án trong bộ nhớ).
    private async Task<List<Question>> PoolAsync(PracticeSnapshotFactory.Criteria criteria)
    {
        var query = _db.Questions.AsNoTracking().Include(x => x.Answers)
            .Where(x => x.SubjectId == criteria.SubjectId && x.Status == 1 && x.QuestionTypeId >= 1 && x.QuestionTypeId <= 3);
        if (criteria.ChapterIds.Count > 0)
            query = query.Where(x => x.ChapterId != null && criteria.ChapterIds.Contains(x.ChapterId.Value));
        if (criteria.QuestionLevelIds.Count > 0)
            query = query.Where(x => x.QuestionLevelId != null && criteria.QuestionLevelIds.Contains(x.QuestionLevelId.Value));
        if (criteria.TagIds.Count > 0)
            query = query.Where(x => x.QuestionTags.Any(t => criteria.TagIds.Contains(t.TagId)));
        var rows = await query.AsSplitQuery().ToListAsync();
        return rows.Where(x => PracticeSnapshotFactory.IsUsable(x, criteria.SubjectId)).ToList();
    }

    private static ObjectResult NotEnough(int available, int requested) => new ConflictObjectResult(new ApiResponse<object>
    {
        Success = false, Message = "PRACTICE_NOT_ENOUGH_QUESTIONS", Data = new { available, requested }
    });

    private async Task<object> StateAsync(PracticeSession row)
    {
        var subjectName = await _db.Subjects.Where(x => x.Id == row.SubjectId).Select(x => x.Name).FirstOrDefaultAsync();
        return new
        {
            id = row.Id, subjectId = row.SubjectId, subjectName, kind = row.Kind, revealMode = row.RevealMode,
            state = row.State, revision = row.Revision, questionCount = row.QuestionCount, correctCount = row.CorrectCount,
            createdAtUtc = row.CreatedAtUtc, finishedAtUtc = row.FinishedAtUtc,
            criteria = JsonSerializer.Deserialize<JsonElement>(row.CriteriaJson),
            // Không có đáp án đúng, điểm hay lời giải trong DTO làm bài
            questions = AttemptSnapshotFactory.ForStudent(Snapshot(row)), answers = Draft(row)
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
                .Where(x => subjectIds.Contains(x.SubjectId) && x.Status == 1 && x.QuestionTypeId >= 1 && x.QuestionTypeId <= 3)
                .AsSplitQuery().ToListAsync())
            .Where(x => PracticeSnapshotFactory.IsUsable(x, x.SubjectId)).ToList();
        var chapters = await _db.Chapters.AsNoTracking().Where(x => subjectIds.Contains(x.SubjectId) && x.Status == 1)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.SubjectId, x.Name }).ToListAsync();
        var levels = await _db.QuestionLevels.AsNoTracking().Where(x => x.Status == true).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var tagIds = questions.SelectMany(x => x.QuestionTags.Select(t => t.TagId)).Distinct().ToList();
        var tags = await _db.Tags.AsNoTracking().Where(x => tagIds.Contains(x.Id) && x.Status == 1).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var result = subjects.Select(subject =>
        {
            var pool = questions.Where(x => x.SubjectId == subject.Id).ToList();
            return new
            {
                id = subject.Id, name = subject.Name, questionCount = pool.Count,
                chapters = chapters.Where(x => x.SubjectId == subject.Id)
                    .Select(x => new { id = x.Id, name = x.Name, questionCount = pool.Count(q => q.ChapterId == x.Id) }).ToList(),
                levels = levels.Select(x => new { id = x.Id, name = x.Name, questionCount = pool.Count(q => q.QuestionLevelId == x.Id) })
                    .Where(x => x.questionCount > 0).ToList(),
                tags = tags.Select(x => new { id = x.Id, name = x.Name, questionCount = pool.Count(q => q.QuestionTags.Any(t => t.TagId == x.Id)) })
                    .Where(x => x.questionCount > 0).ToList()
            };
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            minQuestions = PracticeSnapshotFactory.MinQuestions, maxQuestions = PracticeSnapshotFactory.MaxQuestions,
            maxOpenSessions = PracticeSnapshotFactory.MaxOpenSessions, subjects = result
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePracticeRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var (criteria, error) = PracticeSnapshotFactory.Normalize(request.SubjectId, request.ChapterIds,
            request.QuestionLevelIds, request.TagIds, request.QuestionCount, request.RevealMode);
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
        if (await OpenCountAsync(userId.Value) >= PracticeSnapshotFactory.MaxOpenSessions) return Deny("PRACTICE_TOO_MANY_OPEN");
        var pool = await PoolAsync(criteria);
        if (pool.Count < criteria.QuestionCount) return NotEnough(pool.Count, criteria.QuestionCount);
        var picked = PracticeSnapshotFactory.Pick(pool, criteria.QuestionCount, Random.Shared);
        var row = NewSession(userId.Value, criteria.SubjectId, criteria.RevealMode, criteria, null, picked);
        _db.PracticeSessions.Add(row);
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return StatusCode(201, ApiResponse<object>.Ok(await StateAsync(row)));
    }

    private Task<int> OpenCountAsync(Guid userId) =>
        _db.PracticeSessions.CountAsync(x => x.UserId == userId && x.State == "inProgress");

    private static PracticeSession NewSession(Guid userId, int subjectId, string revealMode,
        PracticeSnapshotFactory.Criteria? criteria, Guid? retryOf, IReadOnlyList<Question> questions) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, SubjectId = subjectId, Kind = "practice", RevealMode = revealMode,
        State = "inProgress", Revision = 0, QuestionCount = questions.Count, CreatedAtUtc = DateTime.UtcNow,
        CriteriaJson = JsonSerializer.Serialize(new
        {
            subjectId, chapterIds = criteria?.ChapterIds ?? Array.Empty<int>(),
            questionLevelIds = criteria?.QuestionLevelIds ?? Array.Empty<int>(), tagIds = criteria?.TagIds ?? Array.Empty<int>(),
            questionCount = questions.Count, revealMode, retryOf
        }, Json),
        SnapshotJson = JsonSerializer.Serialize(PracticeSnapshotFactory.Create(subjectId, questions), Json),
        DraftJson = "[]"
    };

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 50) return BadRequest(ApiResponse<string>.Fail("Trang hoặc số dòng không hợp lệ."));
        var query = _db.PracticeSessions.AsNoTracking().Where(x => x.UserId == userId);
        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                id = x.Id, subjectId = x.SubjectId,
                subjectName = _db.Subjects.Where(s => s.Id == x.SubjectId).Select(s => s.Name).FirstOrDefault(),
                kind = x.Kind, revealMode = x.RevealMode, state = x.State, questionCount = x.QuestionCount,
                correctCount = x.CorrectCount, createdAtUtc = x.CreatedAtUtc, finishedAtUtc = x.FinishedAtUtc
            }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { page, pageSize, totalCount, items }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var row = await Owned(id, userId.Value);
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
        if (row.State != "inProgress") return Deny("PRACTICE_CLOSED");
        if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
        IReadOnlyList<AttemptAnswerDto> draft;
        try { draft = AttemptSnapshotFactory.ValidateDraft(Snapshot(row), request.Answers); }
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
        // Idempotent: phiên đã chốt trả lại đúng kết quả cũ, không kiểm revision
        if (row.State == "inProgress")
        {
            if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
            var score = AttemptScoring.Calculate(Snapshot(row), Draft(row), 0);
            row.CorrectCount = (int)score.RawTotal;
            row.State = "finished";
            row.FinishedAtUtc = DateTime.UtcNow;
            row.Revision++;
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            id = row.Id, state = row.State, revision = row.Revision, questionCount = row.QuestionCount,
            correctCount = row.CorrectCount, finishedAtUtc = row.FinishedAtUtc
        }));
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var row = await _db.PracticeSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (row == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        if (row.State != "finished") return Deny("PRACTICE_NOT_FINISHED");
        var selected = Draft(row).ToDictionary(x => x.QuestionId, x => x.SelectedAnswerIds);
        var questions = Snapshot(row).Questions.Select(question =>
        {
            var picked = selected.TryGetValue(question.Id, out var ids) ? ids : Array.Empty<Guid>();
            var correct = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToHashSet();
            return new
            {
                id = question.Id, questionTypeId = question.QuestionTypeId, content = question.Content,
                selectedAnswerIds = picked, isCorrect = picked.Count > 0 && picked.ToHashSet().SetEquals(correct),
                options = question.Options.Select(option => new { id = option.Id, content = option.Content, isCorrect = option.IsCorrect }).ToList(),
                explanation = question.Explanation
            };
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            id = row.Id, state = row.State, revealMode = row.RevealMode, questionCount = row.QuestionCount,
            correctCount = row.CorrectCount, finishedAtUtc = row.FinishedAtUtc, questions
        }));
    }

    // "Làm lại bộ này" (Q4, QT5-3 a): cùng câu gốc, chụp lại từ ngân hàng hiện tại, bỏ câu không còn dùng được, xáo lại.
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockAsync(userId.Value);
        var source = await _db.PracticeSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (source == null) return NotFound(ApiResponse<string>.Fail(NotFoundCode));
        if (!await AllowedSubjects(userId.Value).AnyAsync(x => x.Id == source.SubjectId))
            return StatusCode(403, ApiResponse<string>.Fail("PRACTICE_SUBJECT_FORBIDDEN"));
        if (await OpenCountAsync(userId.Value) >= PracticeSnapshotFactory.MaxOpenSessions) return Deny("PRACTICE_TOO_MANY_OPEN");
        var sourceIds = Snapshot(source).Questions.Select(x => x.SourceQuestionId).Distinct().ToList();
        var pool = (await _db.Questions.AsNoTracking().Include(x => x.Answers).Where(x => sourceIds.Contains(x.Id))
                .AsSplitQuery().ToListAsync())
            .Where(x => PracticeSnapshotFactory.IsUsable(x, source.SubjectId)).ToList();
        if (pool.Count == 0) return NotEnough(0, sourceIds.Count);
        var picked = PracticeSnapshotFactory.Pick(pool, pool.Count, Random.Shared);
        var row = NewSession(userId.Value, source.SubjectId, source.RevealMode, null, source.Id, picked);
        _db.PracticeSessions.Add(row);
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
        if (row.State != "inProgress") return Deny("PRACTICE_FINISHED");
        _db.PracticeSessions.Remove(row);
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
