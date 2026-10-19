using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminManagement")]
[Route("web/admin/attempts")]
public sealed class AdminAttemptVoidController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly ScheduleWriteGuard _guard;
    public AdminAttemptVoidController(ProjectDACNDbContext db, ScheduleWriteGuard guard)
    { _db = db; _guard = guard; }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidAttemptRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return BadRequest(ApiResponse<string>.Fail("VOID_REASON_REQUIRED"));
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _guard.AcquireAsync();
        var row = await _db.ExamAttempts.Include(x => x.Submission).FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return NotFound(ApiResponse<string>.Fail("ATTEMPT_NOT_FOUND"));
        if (row.Submission?.VoidedAtUtc != null)
        {
            if (row.Submission.VoidReason != reason)
                return Conflict(ApiResponse<string>.Fail("ATTEMPT_ALREADY_VOIDED"));
            await transaction.CommitAsync();
            return Ok(ApiResponse<object>.Ok(new { attemptId = id, state = "voided",
                voidedAt = DateTime.SpecifyKind(row.Submission.VoidedAtUtc.Value, DateTimeKind.Utc),
                voidedByUserId = row.Submission.VoidedByUserId, reason = row.Submission.VoidReason }));
        }
        if (row.State is not ("submitted" or "timedOut") || row.Submission == null)
            return Conflict(ApiResponse<string>.Fail("ATTEMPT_NOT_FINALIZED"));
        row.Submission.VoidedAtUtc = DateTime.UtcNow;
        row.Submission.VoidedByUserId = actor;
        row.Submission.VoidReason = reason;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { attemptId = id, state = "voided",
            voidedAt = row.Submission.VoidedAtUtc, voidedByUserId = actor, reason }));
    }
}

public sealed record VoidAttemptRequest(string? Reason);
