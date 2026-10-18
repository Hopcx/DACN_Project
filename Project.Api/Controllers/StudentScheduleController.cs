using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "Student")]
[Route("web/student/schedules")]
public class StudentScheduleController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public StudentScheduleController(ProjectDACNDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();

        var rows = await _db.ExamSchedules.AsNoTracking()
            .Where(schedule => schedule.Status == 1 && schedule.ClassExamSchedules.Any(link =>
                link.Class.Status == 1 && link.Class.ClassUsers.Any(member =>
                    member.UserId == userId && member.Status == 1)))
            .OrderBy(schedule => schedule.StartTime).ThenBy(schedule => schedule.Id)
            .Select(schedule => new {
                Schedule = schedule,
                RoomName = schedule.Room != null ? schedule.Room.Name : null,
                SubjectName = schedule.Subject != null ? schedule.Subject.Name : null
            })
            .ToListAsync();
        var scheduleIds = rows.Select(x => x.Schedule.Id).ToArray();
        var lastAttempts = await _db.ExamAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && scheduleIds.Contains(x.ExamScheduleId))
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new { x.Id, x.ExamScheduleId, x.State, x.DeadlineUtc })
            .ToListAsync();
        var latestBySchedule = lastAttempts.GroupBy(x => x.ExamScheduleId)
            .ToDictionary(x => x.Key, x => x.First());
        var serverNow = DateTime.UtcNow;
        var result = rows.Select(x => {
            latestBySchedule.TryGetValue(x.Schedule.Id, out var last);
            return new StudentScheduleDto(x.Schedule.Id, x.Schedule.Title,
            x.Schedule.IsTimeUtc ? DateTime.SpecifyKind(x.Schedule.StartTime, DateTimeKind.Utc) : x.Schedule.StartTime,
            x.Schedule.IsTimeUtc ? DateTime.SpecifyKind(x.Schedule.EndTime, DateTimeKind.Utc) : x.Schedule.EndTime,
            x.Schedule.IsTimeUtc ? "utc" : "unknown", x.Schedule.Status, x.Schedule.RoomId,
            x.RoomName, x.SubjectName, serverNow,
            !x.Schedule.IsTimeUtc ? "unknown" : serverNow < x.Schedule.StartTime ? "upcoming" :
                serverNow < x.Schedule.EndTime ? "open" : "ended",
            last?.Id, last == null ? null : last.State == "inProgress" && serverNow >= last.DeadlineUtc ? "timedOut" : last.State);
        }).ToList();
        return Ok(ApiResponse<List<StudentScheduleDto>>.Ok(result));
    }
}

public record StudentScheduleDto(int Id, string? Title, DateTime StartTime, DateTime EndTime,
    string TimeZoneStatus, byte? Status, int? RoomId, string? RoomName, string? SubjectName,
    DateTime ServerNow, string ScheduleState, Guid? LatestAttemptId, string? LatestAttemptState);
