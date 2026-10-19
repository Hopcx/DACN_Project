using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// Historical rows have no attempt snapshot and no verified time-zone offset.
[ApiController]
[Authorize(Policy = "Student")]
[Route("web/legacy-submissions")]
public sealed class LegacySubmissionController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public LegacySubmissionController(ProjectDACNDbContext db) => _db = db;
    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static object PublicRow(Project.Domain.Entities.LegacySubmissionRecord row) => new {
        source = "legacySubmission", id = row.Id, sourceSystem = row.Batch.SourceSystem,
        sourceDatabase = row.Batch.SourceDatabase, sourceTable = row.SourceTable, legacyId = row.SourceId,
        examName = row.ExamName, scheduleTitle = row.ScheduleTitle,
        submittedAtLocalUnknown = row.SubmitTimeUnknown.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
        timeZoneStatus = "unknown", timeTaken = row.TimeTaken.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
        legacyStatus = row.LegacyStatus, hasSnapshot = false, canReviewAnswers = false,
        result = row.AllowViewResult ? new { totalMark = row.TotalMark, isPassed = row.IsPassed } : null
    };

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var actor = Actor(); if (actor == null) return Unauthorized();
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<string>.Fail("INVALID_PAGING"));
        var query = _db.LegacySubmissionRecords.AsNoTracking().Where(x => x.TargetUserId == actor);
        var count = await query.CountAsync();
        var rows = await query.Include(x => x.Batch).OrderByDescending(x => x.SubmitTimeUnknown).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { scope = "legacySubmissionsOnly", page, pageSize,
            totalCount = count, items = rows.Select(PublicRow).ToList() }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var actor = Actor(); if (actor == null) return Unauthorized();
        var row = await _db.LegacySubmissionRecords.AsNoTracking().Include(x => x.Batch)
            .FirstOrDefaultAsync(x => x.Id == id && x.TargetUserId == actor);
        return row == null ? NotFound(ApiResponse<string>.Fail("LEGACY_SUBMISSION_NOT_FOUND"))
            : Ok(ApiResponse<object>.Ok(PublicRow(row)));
    }

    [HttpGet("{id:long}/review")]
    public async Task<IActionResult> Review(long id)
    {
        var actor = Actor(); if (actor == null) return Unauthorized();
        var owned = await _db.LegacySubmissionRecords.AsNoTracking().AnyAsync(x => x.Id == id && x.TargetUserId == actor);
        return !owned ? NotFound(ApiResponse<string>.Fail("LEGACY_SUBMISSION_NOT_FOUND"))
            : Conflict(ApiResponse<string>.Fail("LEGACY_SNAPSHOT_UNAVAILABLE"));
    }
}

[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/legacy-submission-reports")]
public sealed class AdminLegacySubmissionReportController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    public AdminLegacySubmissionReportController(ProjectDACNDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<string>.Fail("INVALID_PAGING"));
        if (status != null && status is not ("accepted" or "notRecognized" or "pending"))
            return BadRequest(ApiResponse<string>.Fail("INVALID_LEGACY_STATUS"));
        var all = _db.LegacySubmissionRecords.AsNoTracking();
        var filtered = status switch {
            "accepted" => all.Where(x => x.LegacyStatus == true),
            "notRecognized" => all.Where(x => x.LegacyStatus == false),
            "pending" => all.Where(x => x.LegacyStatus == null),
            _ => all
        };
        var total = await filtered.CountAsync();
        var accepted = await all.CountAsync(x => x.LegacyStatus == true);
        var notRecognized = await all.CountAsync(x => x.LegacyStatus == false);
        var pending = await all.CountAsync(x => x.LegacyStatus == null);
        var storedScoreSum = await all.SumAsync(x => (double?)x.TotalMark) ?? 0d;
        var rows = await filtered.Include(x => x.Batch).OrderBy(x => x.Batch.SourceDatabase).ThenBy(x => x.SourceId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = rows.Select(x => new {
            x.Id, x.Batch.SourceSystem, x.Batch.SourceDatabase, x.SourceTable, legacyId = x.SourceId,
            x.TargetUserId, x.ExamName, x.LegacyStatus, x.AllowViewResult, x.TotalMark, x.IsPassed,
            submittedAtLocalUnknown = x.SubmitTimeUnknown.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
            timeZoneStatus = "unknown", hasSnapshot = false
        }).ToList();
        return Ok(ApiResponse<object>.Ok(new {
            scope = "legacySubmissionsOnly", page, pageSize, totalCount = total, status,
            summary = new { totalSubmissions = await all.CountAsync(), acceptedStatusCount = accepted,
                notRecognizedStatusCount = notRecognized, pendingStatusCount = pending, storedScoreSum },
            items
        }));
    }
}
