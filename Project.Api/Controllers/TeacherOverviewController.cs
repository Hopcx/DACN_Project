using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// P4: tổng quan cho giảng viên, chỉ đọc. Phạm vi = các lớp do giảng viên phụ trách (Admin: mọi lớp).
// Lượt thi dùng cùng điều kiện như TeacherClassController.Results (lịch gán cho lớp và học viên là thành viên đã duyệt)
// nhưng đếm theo ExamAttempts.Id khác nhau, không nhân đôi khi học viên ở nhiều lớp.
[ApiController]
[Authorize(Policy = "Teacher")]
[Route("web/teacher/overview")]
public sealed class TeacherOverviewController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public TeacherOverviewController(ProjectDACNDbContext db) => _db = db;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private bool TryTeacher(out Guid teacherId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out teacherId);

    // Admin (level 1) là quyền cao nhất: thấy mọi lớp.
    private bool IsAdmin => User.HasClaim("level_id", "1");

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int upcomingLimit = 8)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (upcomingLimit is < 1 or > 50) return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        var isAdmin = IsAdmin;
        var nowUtc = DateTime.UtcNow;
        var ownedClasses = await _db.Classes.AsNoTracking().Where(x => isAdmin || x.TeacherId == teacherId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.ClassCode, x.Status }).ToListAsync();
        var classIds = ownedClasses.Select(x => x.Id).ToList();

        var classes = new { total = ownedClasses.Count, active = ownedClasses.Count(x => x.Status == 1) };

        var memberships = _db.ClassUsers.AsNoTracking().Where(x => classIds.Contains(x.ClassId));
        var members = new
        {
            approvedStudents = await memberships.Where(x => x.Status == 1).Select(x => x.UserId).Distinct().CountAsync(),
            pendingRequests = await memberships.CountAsync(x => x.Status == 2),
        };

        var schedules = _db.ExamSchedules.AsNoTracking().Where(x => x.Status == 1 &&
            _db.ClassExamSchedules.Any(link => link.ExamScheduleId == x.Id && classIds.Contains(link.ClassId)));
        var scheduleSummary = new
        {
            upcomingCount = await schedules.CountAsync(x => x.IsTimeUtc && x.StartTime > nowUtc),
            openCount = await schedules.CountAsync(x => x.IsTimeUtc && x.StartTime <= nowUtc && nowUtc < x.EndTime),
            unknownTimeZoneCount = await schedules.CountAsync(x => !x.IsTimeUtc),
        };

        var attempts = _db.ExamAttempts.AsNoTracking().Where(a =>
            _db.ClassExamSchedules.Any(cs => classIds.Contains(cs.ClassId) && cs.ExamScheduleId == a.ExamScheduleId &&
                _db.ClassUsers.Any(cu => cu.ClassId == cs.ClassId && cu.UserId == a.UserId && cu.Status == 1)));
        var finalized = attempts.Where(x => (x.State == "submitted" || x.State == "timedOut") &&
            x.Submission != null && x.Submission.VoidedAtUtc == null);
        var finalizedCount = await finalized.CountAsync();
        var passedCount = await finalized.CountAsync(x => x.Submission!.IsPassed);
        var averageScore = await finalized.AverageAsync(x => (decimal?)x.Submission!.TotalMark) ?? 0m;
        var attemptSummary = new
        {
            totalAttempts = await attempts.CountAsync(),
            inProgressCount = await attempts.CountAsync(x => x.State == "inProgress"),
            finalizedCount, passedCount, failedCount = finalizedCount - passedCount,
            voidedCount = await attempts.CountAsync(x => x.Submission != null && x.Submission.VoidedAtUtc != null),
            averageScore = decimal.Round(averageScore, 2, MidpointRounding.ToEven),
            passPercent = finalizedCount == 0 ? 0m
                : decimal.Round(100m * passedCount / finalizedCount, 2, MidpointRounding.ToEven),
        };

        var memberCounts = await memberships.GroupBy(x => x.ClassId)
            .Select(g => new { ClassId = g.Key, Approved = g.Count(x => x.Status == 1), Pending = g.Count(x => x.Status == 2) })
            .ToListAsync();
        var classResults = await (from a in finalized
            join cs in _db.ClassExamSchedules.AsNoTracking() on a.ExamScheduleId equals cs.ExamScheduleId
            join cu in _db.ClassUsers.AsNoTracking() on new { cs.ClassId, a.UserId } equals new { cu.ClassId, cu.UserId }
            where classIds.Contains(cs.ClassId) && cu.Status == 1
            group a by cs.ClassId into g
            select new
            {
                ClassId = g.Key, Finalized = g.Count(), Passed = g.Count(x => x.Submission!.IsPassed),
                Average = g.Average(x => x.Submission!.TotalMark)
            }).ToListAsync();
        var perClass = ownedClasses.Select(c =>
        {
            var m = memberCounts.FirstOrDefault(x => x.ClassId == c.Id);
            var r = classResults.FirstOrDefault(x => x.ClassId == c.Id);
            return new
            {
                classId = c.Id, name = c.Name, classCode = c.ClassCode, status = c.Status,
                approvedStudents = m?.Approved ?? 0, pendingRequests = m?.Pending ?? 0,
                finalizedCount = r?.Finalized ?? 0, passedCount = r?.Passed ?? 0,
                averageScore = r == null ? 0m : decimal.Round(r.Average, 2, MidpointRounding.ToEven)
            };
        }).ToList();

        var upcomingRows = await (from schedule in schedules
            where schedule.IsTimeUtc && schedule.EndTime > nowUtc
            join exam in _db.Exams.AsNoTracking() on schedule.ExamId equals exam.Id
            orderby schedule.StartTime, schedule.Id
            select new { schedule.Id, schedule.Title, schedule.StartTime, schedule.EndTime, ExamName = exam.Name })
            .Take(upcomingLimit).ToListAsync();
        var upcomingIds = upcomingRows.Select(x => x.Id).ToList();
        var classLinks = await (from link in _db.ClassExamSchedules.AsNoTracking()
            where upcomingIds.Contains(link.ExamScheduleId) && classIds.Contains(link.ClassId)
            join cls in _db.Classes.AsNoTracking() on link.ClassId equals cls.Id
            select new { link.ExamScheduleId, cls.Id, cls.Name }).ToListAsync();
        var upcoming = upcomingRows.Select(x => new
        {
            scheduleId = x.Id, title = x.Title, examName = x.ExamName,
            startTime = Utc(x.StartTime), endTime = Utc(x.EndTime),
            scheduleState = x.StartTime <= nowUtc ? "open" : "upcoming",
            classes = classLinks.Where(c => c.ExamScheduleId == x.Id).Select(c => new { id = c.Id, name = c.Name }).ToList()
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            scope = "dacnAttemptsOnly", serverNow = Utc(nowUtc),
            classes, members, schedules = scheduleSummary, attempts = attemptSummary,
            perClass, perClassMeaning = "attemptMayCountInEveryOwnedClassOfStudent", upcoming
        }));
    }
}
