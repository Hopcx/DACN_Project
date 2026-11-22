using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>
/// To-be T11: bù <c>QuestionOutcomes</c> cho lượt thi/phiên luyện tập đã chốt trước khi có T11 (hoặc ghi lỗi).
/// Quét lúc khởi động và mỗi 60 giây, mỗi đợt tối đa 200 nguồn mỗi loại; mỗi nguồn lưu riêng (idempotent: nguồn đã có
/// dòng thì bỏ qua; PK chặn trùng). Nguồn lỗi (snapshot hỏng…) được ghi log một lần và bỏ qua tới khi khởi động lại.
/// Tạm dừng nếu DB chưa có migration T11.
/// </summary>
public sealed class QuestionOutcomeBackfillService : BackgroundService
{
    public const string RequiredMigration = "20261122133338_ToBeT11QuestionOutcomes";
    private const int BatchSize = 200;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<QuestionOutcomeBackfillService> _log;
    private readonly HashSet<Guid> _failed = [];
    private bool _migrationWarningLogged;

    public QuestionOutcomeBackfillService(IServiceScopeFactory scopes, ILogger<QuestionOutcomeBackfillService> log)
    { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Question outcome backfill failed; next sweep will retry"); }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> exams, practices;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
            if (!(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Contains(RequiredMigration))
            {
                if (!_migrationWarningLogged) _log.LogWarning("Question outcome migration is not applied; backfill is paused");
                _migrationWarningLogged = true;
                return 0;
            }
            _migrationWarningLogged = false;
            var failed = _failed.ToList();
            exams = await db.ExamAttempts.AsNoTracking()
                .Where(a => (a.State == "submitted" || a.State == "timedOut") && a.Submission != null && !failed.Contains(a.Id) &&
                    !db.QuestionOutcomes.Any(o => o.SourceKind == "exam" && o.SourceId == a.Id))
                .OrderBy(a => a.Submission!.SubmittedAtUtc).ThenBy(a => a.Id).Select(a => a.Id).Take(BatchSize).ToListAsync(cancellationToken);
            practices = await db.PracticeSessions.AsNoTracking()
                .Where(p => p.State == "finished" && p.FinishedAtUtc != null && !failed.Contains(p.Id) &&
                    !db.QuestionOutcomes.Any(o => (o.SourceKind == "practice" || o.SourceKind == "mock") && o.SourceId == p.Id))
                .OrderBy(p => p.FinishedAtUtc).ThenBy(p => p.Id).Select(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);
        }
        var done = 0;
        foreach (var (id, isExam) in exams.Select(x => (x, true)).Concat(practices.Select(x => (x, false))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
                int added;
                if (isExam)
                {
                    var row = await db.ExamAttempts.AsNoTracking().Include(x => x.Submission).FirstAsync(x => x.Id == id, cancellationToken);
                    var subjectId = await db.Exams.Where(x => x.Id == row.ExamId).Select(x => x.SubjectId).FirstAsync(cancellationToken);
                    added = await QuestionOutcomeWriter.AddForAttemptAsync(db, row, subjectId, cancellationToken);
                }
                else
                {
                    var row = await db.PracticeSessions.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
                    added = await QuestionOutcomeWriter.AddForPracticeAsync(db, row, cancellationToken);
                }
                if (added == 0) { _failed.Add(id); continue; }  // snapshot không có câu: không quét lại mãi
                await db.SaveChangesAsync(cancellationToken);
                done++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            // Trùng khóa (2627/2601): nguồn vừa được ghi song song — đã có dữ liệu. Lỗi khác: ghi log, bỏ qua tới khi khởi động lại.
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 }) { }
            catch (Exception ex)
            {
                _failed.Add(id);
                _log.LogError(ex, "Question outcome backfill skipped source {SourceId} until restart", id);
            }
        }
        return done;
    }
}
