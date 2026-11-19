using System.Data;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>
/// To-be T9: chốt phiên Thi thử quá hạn khi học viên không còn gửi request (đóng tab, mất mạng). Quét lúc khởi động và
/// mỗi 30 giây; mỗi phiên chốt trong transaction riêng + app lock của học viên, cùng hàm với chốt lười ở API (idempotent).
/// Tạm dừng nếu DB chưa có migration T6 (bảng PracticeSessions + cột CheckedJson).
/// </summary>
public sealed class ExpiredPracticeFinalizerService : BackgroundService
{
    // Entity PracticeSession cần cột CheckedJson (T6)
    private const string RequiredMigration = "20261118065914_ToBeT6PracticeChecked";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpiredPracticeFinalizerService> _log;
    private bool _migrationWarningLogged;

    public ExpiredPracticeFinalizerService(IServiceScopeFactory scopes, ILogger<ExpiredPracticeFinalizerService> log)
    { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Mock exam timeout sweep failed; next sweep will retry"); }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        List<(Guid Id, Guid UserId)> due;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
            if (!(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Contains(RequiredMigration))
            {
                if (!_migrationWarningLogged) _log.LogWarning("Practice migration is not applied on this database; mock sweep is paused");
                _migrationWarningLogged = true;
                return 0;
            }
            _migrationWarningLogged = false;
            var cutoff = DateTime.UtcNow.AddSeconds(-PracticeTiming.GraceSeconds);
            due = (await db.PracticeSessions.AsNoTracking()
                    .Where(x => x.State == "inProgress" && x.DeadlineUtc != null && x.DeadlineUtc <= cutoff)
                    .OrderBy(x => x.DeadlineUtc).ThenBy(x => x.Id).Select(x => new { x.Id, x.UserId }).Take(200).ToListAsync(cancellationToken))
                .Select(x => (x.Id, x.UserId)).ToList();
        }
        var completed = 0;
        foreach (var (id, userId) in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
                var finalizer = scope.ServiceProvider.GetRequiredService<PracticeFinalizer>();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                await PracticeFinalizer.LockAsync(db, userId);
                var row = await db.PracticeSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (row != null && await finalizer.FinalizeIfExpiredAsync(row, DateTime.UtcNow)) completed++;
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { _log.LogError(ex, "Mock exam {SessionId} timeout finalization failed; next sweep will retry", id); }
        }
        return completed;
    }
}
