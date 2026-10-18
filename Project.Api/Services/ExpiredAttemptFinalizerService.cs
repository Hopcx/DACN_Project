using System.Data;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

// Sweeps on startup and every ten seconds. Waiting never holds a transaction or DB lock.
public sealed class ExpiredAttemptFinalizerService : BackgroundService
{
    private const string RequiredMigration = "20261018063239_Task10TimeoutAndDuration";
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpiredAttemptFinalizerService> _log;
    private bool _migrationWarningLogged;

    public ExpiredAttemptFinalizerService(IServiceScopeFactory scopes,
        ILogger<ExpiredAttemptFinalizerService> log) { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Attempt timeout sweep failed; next sweep will retry"); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> dueIds;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (!applied.Contains(RequiredMigration))
            {
                if (!_migrationWarningLogged)
                    _log.LogWarning("Task10 migration is not applied on this database; timeout sweep is paused");
                _migrationWarningLogged = true;
                return 0;
            }
            _migrationWarningLogged = false;
            var now = DateTime.UtcNow;
            dueIds = await db.ExamAttempts.AsNoTracking()
                .Where(x => x.State == "inProgress" && x.DeadlineUtc <= now)
                .OrderBy(x => x.DeadlineUtc).ThenBy(x => x.Id)
                .Select(x => x.Id).ToListAsync(cancellationToken);
        }

        var completed = 0;
        foreach (var id in dueIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
                var guard = scope.ServiceProvider.GetRequiredService<ScheduleWriteGuard>();
                var finalizer = scope.ServiceProvider.GetRequiredService<AttemptFinalizer>();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                await guard.AcquireAsync();
                var row = await db.ExamAttempts.Include(x => x.Submission)
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                var now = DateTime.UtcNow;
                if (row == null || row.State != "inProgress" || !AttemptTiming.IsExpired(now, row.DeadlineUtc)) continue;
                await finalizer.FinalizeAsync(row, now, true, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // One malformed/locked attempt must not starve other overdue attempts.
                _log.LogError(ex, "Unable to finalize overdue attempt {AttemptId}; retry on next sweep", id);
            }
        }
        if (completed > 0) _log.LogInformation("Finalized {AttemptCount} overdue exam attempts", completed);
        return completed;
    }
}
