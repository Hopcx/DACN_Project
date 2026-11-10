using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// P2 dashboard: read-only aggregates for the admin landing page. Attempt figures use
// the same DACN-only definitions as AdminAttemptReportController (legacy excluded).
[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly ProjectDACNDbContext _db;
    public AdminDashboardController(ProjectDACNDbContext db) => _db = db;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int upcomingLimit = 8)
    {
        if (upcomingLimit is < 1 or > 50) return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        var nowUtc = DateTime.UtcNow;
        // "Hôm nay" theo giờ Việt Nam, quy về khoảng UTC [dayStart, dayEnd).
        var dayStartUtc = (nowUtc + VietnamOffset).Date - VietnamOffset;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var users = _db.Users.AsNoTracking().Where(x => x.Status == 1);
        var counts = new
        {
            students = await users.CountAsync(x => x.LevelId == 4),
            teachers = await users.CountAsync(x => x.LevelId == 3),
            classes = await _db.Classes.AsNoTracking().CountAsync(x => x.Status == 1),
            subjects = await _db.Subjects.AsNoTracking().CountAsync(x => x.Status == 1),
            exams = await _db.Exams.AsNoTracking().CountAsync(),
            questions = await _db.Questions.AsNoTracking().CountAsync(),
        };

        // Chỉ lịch đang kích hoạt và có thời gian UTC rõ ràng; lịch "unknown" không suy ra được hôm nay.
        var schedules = _db.ExamSchedules.AsNoTracking().Where(x => x.Status == 1 && x.IsTimeUtc);
        var todaySchedules = await schedules.CountAsync(x => x.StartTime < dayEndUtc && x.EndTime > dayStartUtc);

        var attempts = _db.ExamAttempts.AsNoTracking();
        var finalized = attempts.Where(x => (x.State == "submitted" || x.State == "timedOut") &&
            x.Submission != null && x.Submission.VoidedAtUtc == null);
        var finalizedCount = await finalized.CountAsync();
        var passedCount = await finalized.CountAsync(x => x.Submission!.IsPassed);
        var attemptSummary = new
        {
            totalAttempts = await attempts.CountAsync(),
            inProgressCount = await attempts.CountAsync(x => x.State == "inProgress"),
            voidedCount = await attempts.CountAsync(x => x.Submission != null && x.Submission.VoidedAtUtc != null),
            timeoutCount = await finalized.CountAsync(x => x.State == "timedOut"),
            finalizedCount, passedCount, failedCount = finalizedCount - passedCount,
            passPercent = finalizedCount == 0 ? 0m
                : decimal.Round(100m * passedCount / finalizedCount, 2, MidpointRounding.ToEven),
        };

        var upcomingRows = await (from schedule in schedules
            where schedule.EndTime > nowUtc
            join exam in _db.Exams.AsNoTracking() on schedule.ExamId equals exam.Id
            join subject in _db.Subjects.AsNoTracking() on exam.SubjectId equals subject.Id into subjectJoin
            from subject in subjectJoin.DefaultIfEmpty()
            join room in _db.Rooms.AsNoTracking() on schedule.RoomId equals room.Id into roomJoin
            from room in roomJoin.DefaultIfEmpty()
            orderby schedule.StartTime, schedule.Id
            select new
            {
                schedule.Id, schedule.Title, schedule.StartTime, schedule.EndTime,
                ExamId = exam.Id, ExamName = exam.Name,
                SubjectName = subject == null ? null : subject.Name,
                RoomName = room == null ? null : room.Name,
            }).Take(upcomingLimit).ToListAsync();

        var scheduleIds = upcomingRows.Select(x => x.Id).ToList();
        var classLinks = await (from link in _db.ClassExamSchedules.AsNoTracking()
            where scheduleIds.Contains(link.ExamScheduleId)
            join cls in _db.Classes.AsNoTracking() on link.ClassId equals cls.Id
            select new { link.ExamScheduleId, cls.Id, cls.Name }).ToListAsync();
        // Số thí sinh = số học viên đã duyệt (khác nhau) thuộc các lớp được gán lịch.
        var candidates = await (from link in _db.ClassExamSchedules.AsNoTracking()
            where scheduleIds.Contains(link.ExamScheduleId)
            join member in _db.ClassUsers.AsNoTracking() on link.ClassId equals member.ClassId
            where member.Status == 1
            select new { link.ExamScheduleId, member.UserId })
            .Distinct().GroupBy(x => x.ExamScheduleId)
            .Select(g => new { ScheduleId = g.Key, Count = g.Count() }).ToListAsync();

        var upcoming = upcomingRows.Select(x => new
        {
            scheduleId = x.Id, title = x.Title, examId = x.ExamId, examName = x.ExamName,
            subjectName = x.SubjectName, roomName = x.RoomName,
            startTime = Utc(x.StartTime), endTime = Utc(x.EndTime),
            scheduleState = x.StartTime <= nowUtc ? "open" : "upcoming",
            classes = classLinks.Where(c => c.ExamScheduleId == x.Id)
                .Select(c => new { id = c.Id, name = c.Name }).ToList(),
            candidateCount = candidates.FirstOrDefault(c => c.ScheduleId == x.Id)?.Count ?? 0,
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            scope = "dacnAttemptsOnly", serverNow = Utc(nowUtc),
            today = new { startUtc = Utc(dayStartUtc), endUtc = Utc(dayEndUtc), timeZone = "Asia/Ho_Chi_Minh" },
            counts, todaySchedules, attempts = attemptSummary, upcoming,
        }));
    }
}
