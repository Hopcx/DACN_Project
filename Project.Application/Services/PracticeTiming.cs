namespace Project.Application.Services;

/// <summary>
/// To-be T9: thời gian của Thi thử (hàm thuần). Hạn nộp = lúc tạo + thời lượng. Cho trễ 5 giây (QT9-2 a) để bù độ trễ
/// mạng: lưu/nộp tới trước hạn + 5 giây vẫn nhận; từ đó phiên bị chốt bằng nháp đã lưu, FinishedAtUtc = hạn nộp.
/// </summary>
public static class PracticeTiming
{
    public const int GraceSeconds = 5;

    public static DateTime Deadline(DateTime createdAtUtc, int durationMinutes) => createdAtUtc.AddMinutes(durationMinutes);

    /// <summary>Phiên có hạn và đã quá hạn + ân hạn → không nhận ghi, phải chốt.</summary>
    public static bool IsExpired(DateTime? deadlineUtc, DateTime nowUtc) =>
        deadlineUtc.HasValue && nowUtc >= deadlineUtc.Value.AddSeconds(GraceSeconds);

    /// <summary>Thời điểm ghi nhận nộp: không muộn hơn hạn (nộp trong ân hạn vẫn tính đúng hạn).</summary>
    public static DateTime FinishedAt(DateTime? deadlineUtc, DateTime nowUtc) =>
        deadlineUtc.HasValue && nowUtc > deadlineUtc.Value ? deadlineUtc.Value : nowUtc;

    /// <summary>Thời lượng gợi ý: 1,5 phút/câu, làm tròn lên bội 5, trong khoảng 5–180.</summary>
    public static int SuggestedMinutes(int questionCount) =>
        Math.Clamp((int)Math.Ceiling(questionCount * 1.5 / 5) * 5, PracticeSnapshotFactory.MinDurationMinutes,
            PracticeSnapshotFactory.MaxDurationMinutes);
}
