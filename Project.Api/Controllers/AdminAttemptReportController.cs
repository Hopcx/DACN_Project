using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// Task13 slice B: new DACN attempts only. Legacy Submission has no verified
// mapping to these rows and must not be unioned into this denominator.
[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/attempt-reports")]
public sealed class AdminAttemptReportController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public AdminAttemptReportController(ProjectDACNDbContext db) => _db = db;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var examIds = _db.ExamAttempts.AsNoTracking().Select(x => x.ExamId).Distinct();
        var scheduleIds = _db.ExamAttempts.AsNoTracking().Select(x => x.ExamScheduleId).Distinct();
        var exams = await _db.Exams.AsNoTracking().Where(x => examIds.Contains(x.Id))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync();
        var subjects = await _db.Subjects.AsNoTracking().Where(x =>
                _db.Exams.Any(exam => examIds.Contains(exam.Id) && exam.SubjectId == x.Id))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync();
        var classIds = _db.ClassExamSchedules.AsNoTracking()
            .Where(x => scheduleIds.Contains(x.ExamScheduleId)).Select(x => x.ClassId).Distinct();
        var classes = await _db.Classes.AsNoTracking().Where(x => classIds.Contains(x.Id))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { exams, subjects, classes }));
    }

    // P2: phổ điểm theo bài thi / lớp / môn. Chỉ lượt DACN đã chốt, chưa hủy (cùng mẫu số với Get).
    // Điểm quy về thang 10 theo Exam.MaximmumMark để so sánh giữa các bài có thang khác nhau.
    // Nhóm "class": lịch được gán cho lớp VÀ học viên là thành viên đã duyệt của lớp đó.
    [HttpGet("score-groups")]
    public async Task<IActionResult> ScoreGroups([FromQuery] string groupBy = "exam",
        [FromQuery] int? examId = null, [FromQuery] int? classId = null, [FromQuery] int? subjectId = null)
    {
        if (groupBy is not ("exam" or "class" or "subject") ||
            examId is <= 0 || classId is <= 0 || subjectId is <= 0)
            return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        IQueryable<ExamAttempt> finalized = _db.ExamAttempts.AsNoTracking().Where(x =>
            (x.State == "submitted" || x.State == "timedOut") &&
            x.Submission != null && x.Submission.VoidedAtUtc == null);
        if (examId.HasValue) finalized = finalized.Where(x => x.ExamId == examId.Value);
        if (subjectId.HasValue)
            finalized = finalized.Where(x => _db.Exams.Any(e => e.Id == x.ExamId && e.SubjectId == subjectId.Value));
        if (classId.HasValue)
            finalized = finalized.Where(x => _db.ClassExamSchedules.Any(link =>
                link.ExamScheduleId == x.ExamScheduleId && link.ClassId == classId.Value));

        var rows = await (from attempt in finalized
            join exam in _db.Exams.AsNoTracking() on attempt.ExamId equals exam.Id
            select new {
                attempt.Id, attempt.UserId, attempt.ExamScheduleId, exam.SubjectId, ExamId = exam.Id,
                Max = exam.MaximmumMark, attempt.Submission!.TotalMark, attempt.Submission.IsPassed
            }).ToListAsync();

        List<(int Key, Guid AttemptId)> keyed;
        Dictionary<int, string> names;
        if (groupBy == "exam")
        {
            keyed = rows.Select(r => (r.ExamId, r.Id)).ToList();
            var ids = keyed.Select(k => k.Key).Distinct().ToList();
            names = await _db.Exams.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        }
        else if (groupBy == "subject")
        {
            keyed = rows.Select(r => (r.SubjectId, r.Id)).ToList();
            var ids = keyed.Select(k => k.Key).Distinct().ToList();
            names = await _db.Subjects.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        }
        else
        {
            var scheduleIds = rows.Select(r => r.ExamScheduleId).Distinct().ToList();
            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var memberships = await (from link in _db.ClassExamSchedules.AsNoTracking()
                where scheduleIds.Contains(link.ExamScheduleId) && (!classId.HasValue || link.ClassId == classId.Value)
                join member in _db.ClassUsers.AsNoTracking() on link.ClassId equals member.ClassId
                where member.Status == 1 && userIds.Contains(member.UserId)
                select new { link.ExamScheduleId, link.ClassId, member.UserId }).Distinct().ToListAsync();
            keyed = (from r in rows
                join m in memberships on new { r.ExamScheduleId, r.UserId } equals new { m.ExamScheduleId, m.UserId }
                select (m.ClassId, r.Id)).Distinct().ToList();
            var ids = keyed.Select(k => k.Key).Distinct().ToList();
            names = await _db.Classes.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        }

        var byId = rows.ToDictionary(r => r.Id);
        var groups = keyed.GroupBy(k => k.Key).Select(g =>
        {
            var items = g.Select(k => byId[k.AttemptId]).ToList();
            var buckets = new int[10];
            foreach (var item in items.Where(i => i.Max > 0))
            {
                var tenScale = (double)item.TotalMark * 10d / item.Max;
                buckets[Math.Clamp((int)Math.Floor(tenScale), 0, 9)]++;
            }
            var passed = items.Count(i => i.IsPassed);
            return new {
                id = g.Key, name = names.GetValueOrDefault(g.Key) ?? $"#{g.Key}",
                finalizedCount = items.Count, passedCount = passed, failedCount = items.Count - passed,
                averageScore = decimal.Round(items.Average(i => i.TotalMark), 2, MidpointRounding.ToEven),
                passPercent = decimal.Round(100m * passed / items.Count, 2, MidpointRounding.ToEven),
                buckets = buckets.Select((count, index) => new {
                    from = index, to = index + 1, label = $"{index}–{index + 1}", count }).ToList()
            };
        }).OrderBy(x => x.name).ToList();

        return Ok(ApiResponse<object>.Ok(new {
            scope = "dacnAttemptsOnly", groupBy, scale = 10,
            classGroupMeaning = "scheduleAssignedToClassAndApprovedMember",
            filters = new { examId, classId, subjectId }, groups
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] int? examId = null, [FromQuery] int? classId = null, [FromQuery] int? subjectId = null,
        [FromQuery] string? state = null)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 ||
            examId is <= 0 || classId is <= 0 || subjectId is <= 0 ||
            (state != null && state is not ("inProgress" or "submitted" or "timedOut" or "voided")))
            return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        IQueryable<ExamAttempt> attempts = _db.ExamAttempts.AsNoTracking();
        if (examId.HasValue) attempts = attempts.Where(x => x.ExamId == examId.Value);
        if (subjectId.HasValue)
            attempts = attempts.Where(x => _db.Exams.Any(e => e.Id == x.ExamId && e.SubjectId == subjectId.Value));
        if (classId.HasValue)
            attempts = attempts.Where(x => _db.ClassExamSchedules.Any(link =>
                link.ExamScheduleId == x.ExamScheduleId && link.ClassId == classId.Value));
        if (state == "voided") attempts = attempts.Where(x => x.Submission != null && x.Submission.VoidedAtUtc != null);
        else if (state != null) attempts = attempts.Where(x => x.State == state &&
            (x.Submission == null || x.Submission.VoidedAtUtc == null));

        // A finalized attempt is one new attempt row in either terminal score state
        // with its unique submission. Hidden results and timeouts are included.
        var finalized = attempts.Where(x => (x.State == "submitted" || x.State == "timedOut") &&
            x.Submission != null && x.Submission.VoidedAtUtc == null);
        var totalAttempts = await attempts.CountAsync();
        var inProgressCount = await attempts.CountAsync(x => x.State == "inProgress");
        var voidedCount = await attempts.CountAsync(x => x.Submission != null && x.Submission.VoidedAtUtc != null);
        var finalizedCount = await finalized.CountAsync();
        var passedCount = await finalized.CountAsync(x => x.Submission!.IsPassed);
        var timeoutCount = await finalized.CountAsync(x => x.State == "timedOut");
        var hiddenResultCount = await finalized.CountAsync(x => !x.AllowViewResult);
        var averageScore = await finalized.AverageAsync(x => (decimal?)x.Submission!.TotalMark) ?? 0m;
        var distribution = await finalized.GroupBy(x => x.Submission!.TotalMark)
            .Select(group => new { score = group.Key, count = group.Count() })
            .OrderBy(x => x.score).ToListAsync();

        var rows = await (from attempt in attempts
            join user in _db.Users.AsNoTracking() on attempt.UserId equals user.Id
            join exam in _db.Exams.AsNoTracking() on attempt.ExamId equals exam.Id
            join schedule in _db.ExamSchedules.AsNoTracking() on attempt.ExamScheduleId equals schedule.Id
            orderby attempt.StartedAtUtc descending, attempt.Id descending
            select new {
                attempt.Id, attempt.UserId, user.UserName,
                attempt.ExamId, ExamName = exam.Name, exam.SubjectId,
                attempt.ExamScheduleId, ScheduleTitle = schedule.Title,
                attempt.StartedAtUtc, attempt.DeadlineUtc, attempt.FinishedAtUtc,
                attempt.State, attempt.AllowViewResult,
                SubmittedAtUtc = attempt.Submission == null ? (DateTime?)null : attempt.Submission.SubmittedAtUtc,
                TotalMark = attempt.Submission == null ? (decimal?)null : attempt.Submission.TotalMark,
                IsPassed = attempt.Submission == null ? (bool?)null : attempt.Submission.IsPassed,
                VoidedAtUtc = attempt.Submission == null ? (DateTime?)null : attempt.Submission.VoidedAtUtc,
                VoidedByUserId = attempt.Submission == null ? (Guid?)null : attempt.Submission.VoidedByUserId,
                VoidReason = attempt.Submission == null ? null : attempt.Submission.VoidReason
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = rows.Select(x => new {
            source = "dacnAttempt", attemptId = x.Id, userId = x.UserId, userName = x.UserName,
            examId = x.ExamId, examName = x.ExamName, subjectId = x.SubjectId,
            scheduleId = x.ExamScheduleId, scheduleTitle = x.ScheduleTitle,
            state = x.VoidedAtUtc.HasValue ? "voided" : x.State, originalState = x.State,
            allowViewResult = x.AllowViewResult,
            startedAt = Utc(x.StartedAtUtc), deadline = Utc(x.DeadlineUtc),
            finishedAt = x.FinishedAtUtc.HasValue ? Utc(x.FinishedAtUtc.Value) : (DateTime?)null,
            submittedAt = x.SubmittedAtUtc.HasValue ? Utc(x.SubmittedAtUtc.Value) : (DateTime?)null,
            totalMark = x.TotalMark, isPassed = x.IsPassed,
            voidedAt = x.VoidedAtUtc.HasValue ? Utc(x.VoidedAtUtc.Value) : (DateTime?)null,
            voidedByUserId = x.VoidedByUserId, voidReason = x.VoidReason
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new {
            scope = "dacnAttemptsOnly", classFilterMeaning = "scheduleAssignedToClass",
            page, pageSize, totalCount = totalAttempts,
            filters = new { examId, classId, subjectId, state },
            summary = new {
                totalAttempts, inProgressCount, voidedCount,
                finalizedCount, submittedCount = finalizedCount - timeoutCount, timeoutCount,
                hiddenResultCount, passedCount, failedCount = finalizedCount - passedCount,
                averageScore = decimal.Round(averageScore, 2, MidpointRounding.ToEven),
                passPercent = finalizedCount == 0 ? 0m : decimal.Round(100m * passedCount / finalizedCount, 2, MidpointRounding.ToEven)
            },
            distribution, items
        }));
    }
}
