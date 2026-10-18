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
        return new {
            attemptId = row.Id, scheduleId = row.ExamScheduleId, state = row.State,
            serverNow = now, deadline = Utc(row.DeadlineUtc), revision = row.Revision,
            questions = finished ? Array.Empty<AttemptQuestionDto>() : AttemptSnapshotFactory.ForStudent(Snapshot(row)),
            savedAnswers = finished ? Array.Empty<AttemptAnswerDto>() : Draft(row),
            result = finished && row.AllowViewResult && result != null
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
