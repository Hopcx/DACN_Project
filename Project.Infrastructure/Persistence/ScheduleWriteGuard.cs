using Microsoft.EntityFrameworkCore;

namespace Project.Infrastructure.Persistence;

// All schedule, assignment and approved-membership writes take this transaction-scoped
// SQL Server application lock before checking overlaps. The lock name is shared by all
// API instances connected to the same database.
public class ScheduleWriteGuard
{
    private readonly ProjectDACNDbContext _db;
    public ScheduleWriteGuard(ProjectDACNDbContext db) => _db = db;

    public async Task AcquireAsync()
    {
        if (_db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Schedule write lock requires a database transaction.");
        await _db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'DACN:schedule-write', @LockMode = N'Exclusive',
                @LockOwner = N'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51009, 'Unable to acquire schedule write lock', 1;
            """);
    }

    public Task<bool> RoomConflictAsync(int? roomId, int? excludedScheduleId,
        DateTime startUtc, DateTime endUtc) => roomId == null
        ? Task.FromResult(false)
        : _db.ExamSchedules.AnyAsync(x => x.Id != excludedScheduleId && x.Status == 1 &&
            x.RoomId == roomId && (!x.IsTimeUtc ||
                (x.StartTime < endUtc && startUtc < x.EndTime)));

    public async Task<bool> StudentConflictAsync(int? excludedScheduleId,
        DateTime startUtc, DateTime endUtc, IReadOnlyCollection<int> classIds)
    {
        if (classIds.Count == 0) return false;
        var candidateUsers = _db.ClassUsers.Where(x => classIds.Contains(x.ClassId) && x.Status == 1)
            .Select(x => x.UserId);
        return await _db.ClassExamSchedules.AnyAsync(link =>
            link.ExamScheduleId != excludedScheduleId && link.ExamSchedule.Status == 1 &&
            (!link.ExamSchedule.IsTimeUtc ||
                (link.ExamSchedule.StartTime < endUtc && startUtc < link.ExamSchedule.EndTime)) &&
            link.Class.ClassUsers.Any(member => member.Status == 1 && candidateUsers.Contains(member.UserId)));
    }

    public async Task<bool> MembershipConflictAsync(int classId, Guid userId)
    {
        var targetSchedules = await _db.ClassExamSchedules.AsNoTracking()
            .Where(link => link.ClassId == classId && link.ExamSchedule.Status == 1)
            .Select(link => new { link.ExamScheduleId, link.ExamSchedule.StartTime,
                link.ExamSchedule.EndTime, link.ExamSchedule.IsTimeUtc })
            .ToListAsync();
        foreach (var target in targetSchedules)
        {
            if (await _db.ClassExamSchedules.AnyAsync(link =>
                link.ExamScheduleId != target.ExamScheduleId && link.ExamSchedule.Status == 1 &&
                (!link.ExamSchedule.IsTimeUtc || !target.IsTimeUtc ||
                    (link.ExamSchedule.StartTime < target.EndTime && target.StartTime < link.ExamSchedule.EndTime)) &&
                link.Class.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)))
                return true;
        }
        return false;
    }

    public async Task<bool> HasAttemptsAsync(int scheduleId) =>
        await _db.DoingExams.AnyAsync(x => x.ExamScheduleId == scheduleId) ||
        await _db.Submissions.AnyAsync(x => x.ExamScheduleId == scheduleId);
}
