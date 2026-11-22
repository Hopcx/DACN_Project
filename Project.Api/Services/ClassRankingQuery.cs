using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

public sealed record RankingSchedule(int Id, string? Title, string ExamName, DateTime StartTime, DateTime EndTime,
    bool IsTimeUtc, bool Counted, string? NotCountedReason);

public sealed record RankingMember(Guid UserId, string UserName, string FullName, string Email);

public sealed record ClassRankingData(IReadOnlyList<RankingSchedule> Schedules, IReadOnlyList<RankingMember> Members,
    RankingResult Result, DateTime ServerNow, IReadOnlyDictionary<(Guid UserId, int ScheduleId), Guid> ChosenAttempts);

/// <summary>
/// To-be T10: đọc dữ liệu xếp hạng một lớp (chỉ đọc, chỉ bài thi — Q7/Q8c). Giảng viên và học viên dùng cùng
/// một kết quả (QT10-1) nên hạng hai bên luôn khớp; controller chỉ quyết định phần nào được trả ra.
/// Lịch được tính: gán cho lớp, Status = 1, giờ UTC, đã kết thúc, bài thi cho xem kết quả và không lượt nào
/// của lịch (đã chốt, chưa hủy) bị tắt xem kết quả. Thành viên: đã duyệt (Status = 1) tại thời điểm đọc.
/// </summary>
public sealed class ClassRankingQuery
{
    public const int MaxMembers = 500;
    public const int MaxSchedules = 200;
    private readonly ProjectDACNDbContext _db;
    public ClassRankingQuery(ProjectDACNDbContext db) => _db = db;

    /// <returns>null khi lớp vượt giới hạn (<see cref="MaxMembers"/>/<see cref="MaxSchedules"/>).</returns>
    public async Task<ClassRankingData?> LoadAsync(int classId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var scheduleCount = await _db.ClassExamSchedules.CountAsync(x => x.ClassId == classId, ct);
        var memberCount = await _db.ClassUsers.CountAsync(x => x.ClassId == classId && x.Status == 1, ct);
        if (!WithinLimits(scheduleCount, memberCount)) return null;

        var rows = await _db.ClassExamSchedules.AsNoTracking().Where(x => x.ClassId == classId)
            .Select(x => new
            {
                x.ExamSchedule.Id, x.ExamSchedule.Title, ExamName = x.ExamSchedule.Exam.Name,
                x.ExamSchedule.StartTime, x.ExamSchedule.EndTime, x.ExamSchedule.IsTimeUtc, x.ExamSchedule.Status,
                ExamAllows = x.ExamSchedule.Exam.AllowViewResult == true,
                // Review T10 (QT10-1): chỉ xét lượt đã chốt, chưa hủy; lượt đang làm/đã hủy không chặn lịch.
                HiddenAttempt = _db.ExamAttempts.Any(a => a.ExamScheduleId == x.ExamScheduleId && !a.AllowViewResult &&
                    (a.State == "submitted" || a.State == "timedOut") && a.Submission != null && a.Submission.VoidedAtUtc == null)
            }).ToListAsync(ct);
        var schedules = rows.GroupBy(x => x.Id).Select(g => g.First())
            .Select(x =>
            {
                var reason = x.Status != 1 ? "inactive"
                    : !x.IsTimeUtc ? "unknownTimeZone"
                    : now < Utc(x.EndTime) ? "notEnded"
                    : !x.ExamAllows || x.HiddenAttempt ? "resultNotReleased"
                    : null;
                return new RankingSchedule(x.Id, x.Title, x.ExamName, Utc(x.StartTime), Utc(x.EndTime), x.IsTimeUtc, reason == null, reason);
            })
            .OrderBy(x => x.EndTime).ThenBy(x => x.Id).ToList();

        var members = await _db.ClassUsers.AsNoTracking().Where(x => x.ClassId == classId && x.Status == 1)
            .Select(x => new RankingMember(x.UserId, x.User.UserName, x.User.FullName, x.User.Email))
            .ToListAsync(ct);
        members = members.GroupBy(x => x.UserId).Select(g => g.First()).ToList();

        var counted = schedules.Where(x => x.Counted).Select(x => x.Id).ToList();
        var memberIds = members.Select(x => x.UserId).ToList();
        // Review T10: tổng điểm câu tính ngay trong SQL (OPENJSON), không kéo SnapshotJson (có cả câu hỏi/lời giải) về API.
        // QT10-2: mẫu số = tổng point > 0 trong snapshot của chính lượt đó; JSON hỏng/không có câu → 0 (lượt bị bỏ, đếm vào skipped).
        var attempts = counted.Count == 0 || memberIds.Count == 0 ? new List<AttemptRow>()
            : await _db.Database.SqlQuery<AttemptRow>($"""
                SELECT a.Id AS AttemptId, s.SubmittedAtUtc, a.UserId, a.ExamScheduleId AS ScheduleId, s.TotalMark, s.IsPassed,
                    CAST(CASE WHEN ISJSON(a.SnapshotJson) = 1 THEN ISNULL((SELECT SUM(q.point) FROM OPENJSON(a.SnapshotJson, '$.questions')
                        WITH (point float '$.point') q WHERE q.point > 0), 0) ELSE 0 END AS decimal(28,6)) AS MaxMark
                FROM dbo.ExamAttempts a
                JOIN dbo.ExamAttemptSubmissions s ON s.ExamAttemptId = a.Id
                WHERE a.ExamScheduleId IN (SELECT CAST(value AS int) FROM OPENJSON({JsonSerializer.Serialize(counted)}))
                  AND a.State IN (N'submitted', N'timedOut') AND s.VoidedAtUtc IS NULL
                  AND EXISTS (SELECT 1 FROM dbo.ClassUsers cu WHERE cu.ClassId = {classId} AND cu.Status = 1 AND cu.UserId = a.UserId)
                """).ToListAsync(ct);
        var ranking = attempts.Select(a => new RankingAttempt(a.UserId, a.ScheduleId, a.TotalMark, a.MaxMark, a.IsPassed,
            a.AttemptId, DateTime.SpecifyKind(a.SubmittedAtUtc, DateTimeKind.Utc))).ToList();
        var result = ClassRanking.Calculate(counted, memberIds, ranking);
        return new ClassRankingData(schedules, members, result, now, ClassRanking.ChooseAttempts(ranking));
    }

    public static bool WithinLimits(int scheduleCount, int memberCount) =>
        scheduleCount <= MaxSchedules && memberCount <= MaxMembers;

    private sealed class AttemptRow
    {
        public Guid AttemptId { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public Guid UserId { get; set; }
        public int ScheduleId { get; set; }
        public decimal TotalMark { get; set; }
        public bool IsPassed { get; set; }
        public decimal MaxMark { get; set; }
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
