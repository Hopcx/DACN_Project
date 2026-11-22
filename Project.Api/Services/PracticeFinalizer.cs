using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>
/// To-be T5/T9: chốt phiên luyện tập/thi thử — dùng chung cho API (nộp, chốt lười khi quá hạn) và worker nền.
/// Người gọi phải đang trong transaction và giữ app lock của học viên (<see cref="LockAsync"/>). Idempotent:
/// phiên đã chốt không bị chấm lại.
/// </summary>
public sealed class PracticeFinalizer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ProjectDACNDbContext _db;
    public PracticeFinalizer(ProjectDACNDbContext db) => _db = db;

    /// <summary>App lock theo học viên (tuần tự hóa mọi thao tác ghi phiên của một người, kể cả worker).</summary>
    public static async Task LockAsync(ProjectDACNDbContext db, Guid userId)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Practice lock requires a transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {"DACN:practice:" + userId.ToString("N")},
                @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51010, 'Unable to acquire practice lock', 1;
            """);
    }

    /// <summary>Chấm theo nháp đã lưu (tất cả-hoặc-không), đóng phiên và thêm kết quả từng câu (T11). Không SaveChanges.</summary>
    public static async Task ScoreAsync(ProjectDACNDbContext db, PracticeSession row, DateTime finishedAtUtc)
    {
        if (row.State != "inProgress") return;
        var snapshot = JsonSerializer.Deserialize<AttemptSnapshot>(row.SnapshotJson, Json)!;
        var draft = JsonSerializer.Deserialize<List<AttemptAnswerDto>>(row.DraftJson, Json)!;
        row.CorrectCount = (int)AttemptScoring.Calculate(snapshot, draft, 0).RawTotal;
        row.State = "finished";
        row.FinishedAtUtc = finishedAtUtc;
        row.Revision++;
        await QuestionOutcomeWriter.AddForPracticeAsync(db, row);
    }

    /// <summary>Thi thử quá hạn + ân hạn: chốt bằng nháp đã lưu, FinishedAtUtc = hạn nộp. Trả true nếu vừa chốt.</summary>
    public async Task<bool> FinalizeIfExpiredAsync(PracticeSession row, DateTime nowUtc)
    {
        if (row.State != "inProgress" || !PracticeTiming.IsExpired(row.DeadlineUtc, nowUtc)) return false;
        await ScoreAsync(_db, row, row.DeadlineUtc!.Value);
        await _db.SaveChangesAsync();
        return true;
    }
}
