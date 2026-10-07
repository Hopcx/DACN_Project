using System.Data;
using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence;

public enum MembershipOutcome { Created, Updated, Removed, MissingClass, InactiveClass, MissingStudent, Duplicate, Full, MissingMembership, InvalidState, ScheduleConflict }
public record MembershipResult(MembershipOutcome Outcome, ClassUser? Member = null);

// Every membership write takes an update lock on its class row. This serializes
// count/check/write across requests, including requests for different students.
public class ClassMembershipStore
{
    private readonly ProjectDACNDbContext _db;
    private readonly ScheduleWriteGuard _scheduleGuard;
    public ClassMembershipStore(ProjectDACNDbContext db, ScheduleWriteGuard scheduleGuard)
    { _db = db; _scheduleGuard = scheduleGuard; }

    public async Task<MembershipResult> JoinAsync(int classId, Guid studentId, bool approve, string? expectedCode = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        if (approve) await _scheduleGuard.AcquireAsync();
        var schoolClass = await LockedClassAsync(classId);
        if (schoolClass == null) return new(MembershipOutcome.MissingClass);
        if (expectedCode != null && schoolClass.ClassCode != expectedCode) return new(MembershipOutcome.MissingClass);
        if (schoolClass.Status != 1) return new(MembershipOutcome.InactiveClass);
        if (!await _db.Users.AnyAsync(x => x.Id == studentId && x.LevelId == 4 && x.Status == 1 && x.EmailVerifiedAt != null))
            return new(MembershipOutcome.MissingStudent);
        if (await _db.ClassUsers.AnyAsync(x => x.ClassId == classId && x.UserId == studentId))
            return new(MembershipOutcome.Duplicate);
        var occupied = await _db.ClassUsers.CountAsync(x => x.ClassId == classId && x.Status == 1);
        if (occupied >= schoolClass.Capacity) return new(MembershipOutcome.Full);
        if (approve && await _scheduleGuard.MembershipConflictAsync(classId, studentId))
            return new(MembershipOutcome.ScheduleConflict);
        var member = new ClassUser { ClassId = classId, UserId = studentId, Status = approve ? (byte)1 : (byte)2 };
        _db.ClassUsers.Add(member);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(MembershipOutcome.Created, member);
    }

    public async Task<MembershipResult> ApproveAsync(int id)
    {
        var classId = await _db.ClassUsers.Where(x => x.Id == id).Select(x => (int?)x.ClassId).FirstOrDefaultAsync();
        if (classId == null) return new(MembershipOutcome.MissingMembership);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _scheduleGuard.AcquireAsync();
        var schoolClass = await LockedClassAsync(classId.Value);
        if (schoolClass == null || schoolClass.Status != 1) return new(MembershipOutcome.InactiveClass);
        var member = await _db.ClassUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (member == null) return new(MembershipOutcome.MissingMembership);
        if (member.Status != 2) return new(MembershipOutcome.InvalidState);
        if (!await _db.Users.AnyAsync(x => x.Id == member.UserId && x.LevelId == 4 && x.Status == 1 && x.EmailVerifiedAt != null))
            return new(MembershipOutcome.MissingStudent);
        var occupied = await _db.ClassUsers.CountAsync(x => x.ClassId == classId && x.Status == 1);
        if (occupied >= schoolClass.Capacity) return new(MembershipOutcome.Full);
        if (await _scheduleGuard.MembershipConflictAsync(classId.Value, member.UserId))
            return new(MembershipOutcome.ScheduleConflict);
        member.Status = 1;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(MembershipOutcome.Updated, member);
    }

    public async Task<MembershipResult> RemoveAsync(int classId, Guid studentId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        if (await LockedClassAsync(classId) == null) return new(MembershipOutcome.MissingClass);
        var member = await _db.ClassUsers.FirstOrDefaultAsync(x => x.ClassId == classId && x.UserId == studentId);
        if (member == null) return new(MembershipOutcome.MissingMembership);
        _db.ClassUsers.Remove(member);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(MembershipOutcome.Removed, member);
    }

    private Task<Class?> LockedClassAsync(int id) => _db.Classes
        .FromSqlInterpolated($"SELECT * FROM [Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
        .FirstOrDefaultAsync();
}
