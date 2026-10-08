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
        var result = rows.Select(x => new StudentScheduleDto(x.Schedule.Id, x.Schedule.Title,
            x.Schedule.IsTimeUtc ? DateTime.SpecifyKind(x.Schedule.StartTime, DateTimeKind.Utc) : x.Schedule.StartTime,
            x.Schedule.IsTimeUtc ? DateTime.SpecifyKind(x.Schedule.EndTime, DateTimeKind.Utc) : x.Schedule.EndTime,
            x.Schedule.IsTimeUtc ? "utc" : "unknown", x.Schedule.Status, x.Schedule.RoomId,
            x.RoomName, x.SubjectName)).ToList();
        return Ok(ApiResponse<List<StudentScheduleDto>>.Ok(result));
    }
}

public record StudentScheduleDto(int Id, string? Title, DateTime StartTime, DateTime EndTime,
    string TimeZoneStatus, byte? Status, int? RoomId, string? RoomName, string? SubjectName);
