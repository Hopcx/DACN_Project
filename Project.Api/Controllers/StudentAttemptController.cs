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
using Project.Api.Services;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "Student")]
[Route("web/attempts")]
public class StudentAttemptController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly ScheduleWriteGuard _guard;
    private readonly AttemptFinalizer _finalizer;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public StudentAttemptController(ProjectDACNDbContext db, ScheduleWriteGuard guard, AttemptFinalizer finalizer)
    { _db = db; _guard = guard; _finalizer = finalizer; }

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static IActionResult Deny(string code) => new ConflictObjectResult(ApiResponse<string>.Fail(code));
    private static AttemptSnapshot Snapshot(ExamAttempt row) => JsonSerializer.Deserialize<AttemptSnapshot>(row.SnapshotJson, Json)!;
    private static IReadOnlyList<AttemptAnswerDto> Draft(ExamAttempt row) =>
        JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.DraftJson, Json)!;

    private object PublicState(ExamAttempt row, DateTime now)
    {
        var finished = row.State != "inProgress";
        var result = row.Submission;
        var voided = result?.VoidedAtUtc != null;
        return new {
            attemptId = row.Id, scheduleId = row.ExamScheduleId, state = voided ? "voided" : row.State,
            serverNow = now, deadline = Utc(row.DeadlineUtc), revision = row.Revision,
            questions = finished ? Array.Empty<AttemptQuestionDto>() : AttemptSnapshotFactory.ForStudent(Snapshot(row)),
            savedAnswers = finished ? Array.Empty<AttemptAnswerDto>() : Draft(row),
            result = finished && !voided && row.AllowViewResult && result != null
                ? new { totalMark = result.TotalMark, isPassed = result.IsPassed,
                    submittedAt = Utc(result.SubmittedAtUtc), timeTakenSeconds = result.TimeTakenSeconds }
                : null
        };
    }

    private async Task<ExamAttempt?> Owned(Guid id, Guid userId) =>
        await _db.ExamAttempts.Include(x => x.Submission).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId &&
            _db.ClassExamSchedules.Any(link => link.ExamScheduleId == x.ExamScheduleId &&
                link.Class.Status == 1 && link.Class.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)));

    // All attempt mutations and timeout finalization use the same transaction-scoped SQL lock.
    private async Task FinishIfExpired(ExamAttempt row, DateTime now)
    {
        if (row.State == "inProgress" && AttemptTiming.IsExpired(now, row.DeadlineUtc))
            await _finalizer.FinalizeAsync(row, now, true);
    }

    // New DACN attempts only. Legacy DoingExam/Submission have no trustworthy snapshot
    // linkage and are deliberately excluded until Task12 reconciliation.
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? state = null)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 ||
            (state != null && state is not ("inProgress" or "submitted" or "timedOut" or "voided")))
            return BadRequest(ApiResponse<string>.Fail("INVALID_PAGING"));
        var query = _db.ExamAttempts.AsNoTracking().Where(x => x.UserId == userId.Value);
        if (state == "voided") query = query.Where(x => x.Submission != null && x.Submission.VoidedAtUtc != null);
        else if (state != null) query = query.Where(x => x.State == state &&
            (x.Submission == null || x.Submission.VoidedAtUtc == null));
        var totalCount = await query.CountAsync();
        var rows = await (from attempt in query
            join schedule in _db.ExamSchedules.AsNoTracking() on attempt.ExamScheduleId equals schedule.Id
            join exam in _db.Exams.AsNoTracking() on attempt.ExamId equals exam.Id
            orderby attempt.StartedAtUtc descending, attempt.Id descending
            select new {
                attempt.Id, attempt.ExamScheduleId, attempt.ExamId,
                ExamName = exam.Name, ScheduleTitle = schedule.Title,
                attempt.StartedAtUtc, attempt.DeadlineUtc, attempt.FinishedAtUtc,
                attempt.State, attempt.AllowViewResult,
                SubmittedAtUtc = attempt.Submission == null ? (DateTime?)null : attempt.Submission.SubmittedAtUtc,
                TotalMark = attempt.Submission == null ? (decimal?)null : attempt.Submission.TotalMark,
                IsPassed = attempt.Submission == null ? (bool?)null : attempt.Submission.IsPassed,
                TimeTakenSeconds = attempt.Submission == null ? (long?)null : attempt.Submission.TimeTakenSeconds,
                VoidedAtUtc = attempt.Submission == null ? (DateTime?)null : attempt.Submission.VoidedAtUtc
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var now = DateTime.UtcNow;
        var items = rows.Select(x => new {
            attemptId = x.Id, scheduleId = x.ExamScheduleId, examId = x.ExamId,
            examName = x.ExamName, scheduleTitle = x.ScheduleTitle,
            state = x.VoidedAtUtc.HasValue ? "voided" : x.State,
            pendingFinalization = x.State == "inProgress" && AttemptTiming.IsExpired(now, x.DeadlineUtc),
            startedAt = Utc(x.StartedAtUtc), deadline = Utc(x.DeadlineUtc),
            finishedAt = x.FinishedAtUtc.HasValue ? Utc(x.FinishedAtUtc.Value) : (DateTime?)null,
            submittedAt = x.SubmittedAtUtc.HasValue ? Utc(x.SubmittedAtUtc.Value) : (DateTime?)null,
            result = !x.VoidedAtUtc.HasValue && x.AllowViewResult && x.TotalMark.HasValue && x.IsPassed.HasValue
                ? new { totalMark = x.TotalMark.Value, isPassed = x.IsPassed.Value,
                    timeTakenSeconds = x.TimeTakenSeconds }
                : null
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new { scope = "dacnAttemptsOnly", page, pageSize, totalCount, state, items }));
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        if (row.Submission?.VoidedAtUtc != null) return StatusCode(403, ApiResponse<string>.Fail("ATTEMPT_VOIDED"));
        if (row.State == "inProgress" || row.Submission == null)
            return Conflict(ApiResponse<string>.Fail("ATTEMPT_NOT_FINALIZED"));
        if (!row.AllowViewResult) return StatusCode(403, ApiResponse<string>.Fail("RESULT_NOT_RELEASED"));
        var schedule = await _db.ExamSchedules.AsNoTracking().FirstAsync(x => x.Id == row.ExamScheduleId);
        if (!schedule.IsTimeUtc || DateTime.UtcNow < Utc(schedule.EndTime))
            return StatusCode(403, ApiResponse<string>.Fail("REVIEW_NOT_RELEASED"));
        var snapshot = Snapshot(row);
        var answers = JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.Submission.AnswersJson, Json)
            ?? new List<AttemptAnswerDto>();
        var selected = answers.ToDictionary(x => x.QuestionId, x => x.SelectedAnswerIds);
        var questions = snapshot.Questions.Select(question => new {
            id = question.Id, questionTypeId = question.QuestionTypeId, content = question.Content,
            selectedAnswerIds = selected.TryGetValue(question.Id, out var ids) ? ids : Array.Empty<Guid>(),
            options = question.Options.Select(option => new {
                id = option.Id, content = option.Content, isCorrect = option.IsCorrect
            }).ToList()
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new {
            attemptId = row.Id, state = row.State, reviewedFrom = "attemptSnapshot",
            questions
        }));
    }

    [HttpPost("/web/exam-schedules/{scheduleId:int}/attempts")]
    public async Task<IActionResult> Start(int scheduleId)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var schedule = await _db.ExamSchedules.Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == scheduleId);
        if (schedule == null) return NotFound(ApiResponse<string>.Fail("SCHEDULE_NOT_FOUND"));
        if (!await _db.ClassExamSchedules.AnyAsync(link => link.ExamScheduleId == scheduleId &&
            link.Class.Status == 1 && link.Class.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)))
            return Forbid();
        if (!schedule.IsTimeUtc) return Deny("SCHEDULE_TIME_UNKNOWN");
        var now = DateTime.UtcNow;
        var open = await _db.ExamAttempts.Include(x => x.Submission)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ExamScheduleId == scheduleId && x.State == "inProgress");
        if (open != null)
        {
            await FinishIfExpired(open, now);
            // Commit an expired attempt before evaluating another start. A later request may
            // consume a remaining slot; a limit response must never roll back finalization.
            await tx.CommitAsync();
            return Ok(ApiResponse<object>.Ok(PublicState(open, now)));
        }
        if (schedule.Status != 1 || schedule.Exam.Status != 1 ||
            !AttemptTiming.CanStart(now, schedule.StartTime, schedule.EndTime))
            return Deny("EXAM_NOT_OPEN");
        // Legacy rows cannot safely be paired into attempts. Resolve them before allowing a new start.
        if (await _db.DoingExams.AnyAsync(x => x.UserId == userId && x.ExamScheduleId == scheduleId) ||
            await _db.Submissions.AnyAsync(x => x.UserId == userId && x.ExamScheduleId == scheduleId))
            return Deny("LEGACY_ATTEMPT_RECONCILIATION_REQUIRED");
        if (schedule.Exam.NumberOfRepeat < 1 || schedule.Exam.Duration <= 0 || schedule.Exam.ScoreMethodId.HasValue)
            return Deny("EXAM_CONFIGURATION_INVALID");
        var used = await _db.ExamAttempts.CountAsync(x => x.UserId == userId && x.ExamScheduleId == scheduleId);
        if (used >= schedule.Exam.NumberOfRepeat) return Deny("ATTEMPT_LIMIT_REACHED");
        var variants = await _db.ExamDetails.Where(x => x.ExamId == schedule.ExamId && x.Status == 1)
            .Include(x => x.ExamDetailQuestions).ThenInclude(x => x.Question).ThenInclude(x => x.Answers)
            .AsSplitQuery()
            .OrderBy(x => x.Id).ToListAsync();
        if (variants.Count == 0) return Deny("NO_PUBLISHED_VARIANT");
        AttemptSnapshot snapshot;
        try {
            snapshot = AttemptSnapshotFactory.Create(schedule.Exam, variants[used % variants.Count]);
            AttemptScoring.ValidateConfiguration(snapshot, schedule.Exam.PassMark);
        }
        catch (ArgumentException) { return Deny("EXAM_CONFIGURATION_INVALID"); }
        var deadline = AttemptTiming.Deadline(now, schedule.Exam.Duration, Utc(schedule.EndTime));
        var row = new ExamAttempt {
            Id = Guid.NewGuid(), UserId = userId.Value, ExamScheduleId = scheduleId,
            ExamId = schedule.ExamId, ExamDetailId = snapshot.ExamDetailId,
            StartedAtUtc = now, DeadlineUtc = deadline, Revision = 0,
            SnapshotJson = JsonSerializer.Serialize(snapshot, Json),
            PassMark = schedule.Exam.PassMark,
            AllowViewResult = schedule.Exam.AllowViewResult == true
        };
        _db.ExamAttempts.Add(row);
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(PublicState(row, now)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        var now = DateTime.UtcNow;
        await FinishIfExpired(row, now);
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(PublicState(row, now)));
    }

    [HttpGet("{id:guid}/visibility-count")]
    public async Task<IActionResult> VisibilityCount(Guid id)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        if (await Owned(id, userId.Value) == null)
            return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        return Ok(ApiResponse<object>.Ok(new { count = await _db.AttemptVisibilityEvents.CountAsync(x => x.AttemptId == id && x.IsCounted) }));
    }

    [HttpPost("{id:guid}/visibility-hidden")]
    public async Task<IActionResult> VisibilityHidden(Guid id, [FromBody] VisibilityHiddenRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        if (request.EventId == Guid.Empty) return BadRequest(ApiResponse<string>.Fail("INVALID_EVENT_ID"));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        var count = await _db.AttemptVisibilityEvents.CountAsync(x => x.AttemptId == id && x.IsCounted);
        if (await _db.AttemptVisibilityEvents.AnyAsync(x => x.AttemptId == id && x.EventId == request.EventId))
        {
            await tx.CommitAsync();
            return Ok(ApiResponse<object>.Ok(new { count, duplicate = true }));
        }
        var now = DateTime.UtcNow;
        if (row.State != "inProgress" || AttemptTiming.IsExpired(now, row.DeadlineUtc))
            return Conflict(ApiResponse<string>.Fail("ATTEMPT_CLOSED"));
        // Simultaneous hidden events from separate tabs count as one event.
        var recent = await _db.AttemptVisibilityEvents.AnyAsync(x => x.AttemptId == id && x.IsCounted &&
            x.RecordedAtUtc >= now.AddSeconds(-1));
        _db.AttemptVisibilityEvents.Add(new AttemptVisibilityEvent
        { AttemptId = id, EventId = request.EventId, RecordedAtUtc = now, IsCounted = !recent });
        await _db.SaveChangesAsync();
        if (!recent) count++;
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { count, duplicate = recent }));
    }

    [HttpPut("{id:guid}/answers")]
    public async Task<IActionResult> Save(Guid id, [FromBody] SaveAttemptRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        var now = DateTime.UtcNow;
        await FinishIfExpired(row, now);
        if (row.State != "inProgress") { await tx.CommitAsync(); return Deny("ATTEMPT_CLOSED"); }
        if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
        IReadOnlyList<AttemptAnswerDto> draft;
        try { draft = AttemptSnapshotFactory.ValidateDraft(Snapshot(row), request.Answers); }
        catch (ArgumentException) { return BadRequest(ApiResponse<string>.Fail("INVALID_SELECTION")); }
        row.DraftJson = JsonSerializer.Serialize(draft, Json);
        row.Revision++;
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(PublicState(row, now)));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitAttemptRequest request)
    {
        var userId = Actor(); if (userId == null) return Unauthorized();
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var row = await Owned(id, userId.Value);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        var now = DateTime.UtcNow;
        await FinishIfExpired(row, now);
        if (row.State == "inProgress")
        {
            if (request.Revision != row.Revision) return Deny("CONCURRENCY_CONFLICT");
            await _finalizer.FinalizeAsync(row, now, false);
        }
        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(PublicState(row, now)));
    }
}

public sealed record SaveAttemptRequest(int Revision, IReadOnlyList<AttemptAnswerDto>? Answers);
public sealed record SubmitAttemptRequest(int Revision);
public sealed record VisibilityHiddenRequest(Guid EventId);
