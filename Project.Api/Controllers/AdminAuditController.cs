using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// P4/F23: API chỉ đọc cho nhật ký hệ thống (Logs) và nhật ký hoạt động thi (ExamActivityLogs).
[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/audit")]
public sealed class AdminAuditController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public AdminAuditController(ProjectDACNDbContext db) => _db = db;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? action = null, [FromQuery] Guid? actorUserId = null,
        [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        search = search?.Trim(); action = action?.Trim();
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 ||
            (search != null && search.Length > 100) || (action != null && action.Length > 100) ||
            (actorUserId.HasValue && actorUserId.Value == Guid.Empty) ||
            (fromUtc.HasValue && fromUtc.Value.Kind != DateTimeKind.Utc) ||
            (toUtc.HasValue && toUtc.Value.Kind != DateTimeKind.Utc) ||
            (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value))
            return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        var logs = _db.Logs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(search)) logs = logs.Where(x => x.Name.Contains(search) || x.Description.Contains(search));
        if (!string.IsNullOrEmpty(action)) logs = logs.Where(x => x.Action == action);
        if (actorUserId.HasValue) logs = logs.Where(x => x.ActorUserId == actorUserId.Value);
        if (fromUtc.HasValue) logs = logs.Where(x => x.CreatedAtUtc != null && x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) logs = logs.Where(x => x.CreatedAtUtc != null && x.CreatedAtUtc < toUtc.Value);

        var totalCount = await logs.CountAsync();
        var rows = await (from log in logs
            join user in _db.Users.AsNoTracking() on log.ActorUserId equals user.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            orderby log.CreatedAtUtc descending, log.Id descending
            select new {
                log.Id, log.Name, log.Description, log.Status, log.Action, log.EntityType, log.EntityId,
                log.ActorUserId, ActorUserName = user == null ? null : user.UserName,
                log.CreatedAtUtc, log.CorrelationId
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = rows.Select(x => new {
            id = x.Id, name = x.Name, description = x.Description, status = x.Status,
            action = x.Action, entityType = x.EntityType, entityId = x.EntityId,
            actorUserId = x.ActorUserId, actorUserName = x.ActorUserName,
            createdAt = x.CreatedAtUtc.HasValue ? Utc(x.CreatedAtUtc.Value) : (DateTime?)null,
            correlationId = x.CorrelationId
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new {
            page, pageSize, totalCount,
            filters = new { search, action, actorUserId, fromUtc, toUtc }, items
        }));
    }

    // ActionTime của hàng cũ không rõ múi giờ: trả nguyên giá trị, không gắn Z, lọc [from, to) nguyên trạng.
    [HttpGet("exam-activity")]
    public async Task<IActionResult> ExamActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null, [FromQuery] int? examId = null, [FromQuery] int? examScheduleId = null,
        [FromQuery] string? actionType = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        actionType = actionType?.Trim();
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 ||
            (userId.HasValue && userId.Value == Guid.Empty) || examId is <= 0 || examScheduleId is <= 0 ||
            (actionType != null && actionType.Length > 100) ||
            (from.HasValue && to.HasValue && from.Value > to.Value))
            return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));

        var activities = _db.ExamActivityLogs.AsNoTracking().AsQueryable();
        if (userId.HasValue) activities = activities.Where(x => x.UserId == userId.Value);
        if (examId.HasValue) activities = activities.Where(x => x.ExamId == examId.Value);
        if (examScheduleId.HasValue) activities = activities.Where(x => x.ExamScheduleId == examScheduleId.Value);
        if (!string.IsNullOrEmpty(actionType)) activities = activities.Where(x => x.ActionType == actionType);
        if (from.HasValue) activities = activities.Where(x => x.ActionTime >= from.Value);
        if (to.HasValue) activities = activities.Where(x => x.ActionTime < to.Value);

        var totalCount = await activities.CountAsync();
        var items = await (from activity in activities
            join user in _db.Users.AsNoTracking() on activity.UserId equals user.Id
            join exam in _db.Exams.AsNoTracking() on activity.ExamId equals exam.Id
            join schedule in _db.ExamSchedules.AsNoTracking() on activity.ExamScheduleId equals (int?)schedule.Id into scheduleJoin
            from schedule in scheduleJoin.DefaultIfEmpty()
            orderby activity.ActionTime descending, activity.Id descending
            select new {
                id = activity.Id, userId = activity.UserId, userName = user.UserName, fullName = user.FullName,
                examId = activity.ExamId, examName = exam.Name, examDetailId = activity.ExamDetailId,
                examScheduleId = activity.ExamScheduleId, scheduleTitle = schedule == null ? null : schedule.Title,
                actionTime = activity.ActionTime, actionType = activity.ActionType
            }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new {
            page, pageSize, totalCount,
            filters = new { userId, examId, examScheduleId, actionType, from, to }, items
        }));
    }
}
