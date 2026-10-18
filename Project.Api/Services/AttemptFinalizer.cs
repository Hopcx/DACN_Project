using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

// Call inside a transaction after acquiring ScheduleWriteGuard. Controller and worker use
// this exact path; the caller commits state + unique submission together.
public sealed class AttemptFinalizer
{
    private readonly ProjectDACNDbContext _db;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public AttemptFinalizer(ProjectDACNDbContext db) => _db = db;

    public async Task<bool> FinalizeAsync(ExamAttempt row, DateTime serverNowUtc, bool timedOut,
        CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Finalization requires a SQL transaction and schedule lock.");
        if (row.State != "inProgress")
        {
            if (row.Submission == null) throw new InvalidOperationException("Finalized attempt has no result.");
            return false;
        }
        if (serverNowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        if (timedOut != AttemptTiming.IsExpired(serverNowUtc, row.DeadlineUtc))
            throw new InvalidOperationException("Finalization reason disagrees with the fixed deadline.");

        var snapshot = JsonSerializer.Deserialize<AttemptSnapshot>(row.SnapshotJson, Json)
            ?? throw new InvalidOperationException("Attempt snapshot is missing.");
        var draft = JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.DraftJson, Json)
            ?? throw new InvalidOperationException("Attempt draft is missing.");
        var score = AttemptScoring.Calculate(snapshot, draft, row.PassMark);
        var submittedAtUtc = timedOut ? DateTime.SpecifyKind(row.DeadlineUtc, DateTimeKind.Utc) : serverNowUtc;
        var startedAtUtc = DateTime.SpecifyKind(row.StartedAtUtc, DateTimeKind.Utc);
        var elapsedSeconds = Math.Max(0, Math.Floor((submittedAtUtc - startedAtUtc).TotalSeconds));
        row.State = timedOut ? "timedOut" : "submitted";
        row.FinishedAtUtc = serverNowUtc;
        row.Submission = new ExamAttemptSubmission {
            ExamAttemptId = row.Id,
            SubmittedAtUtc = submittedAtUtc,
            TimeTakenSeconds = checked((long)elapsedSeconds),
            TotalMark = score.StoredTotal,
            IsPassed = score.IsPassed,
            AnswersJson = row.DraftJson
        };
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
