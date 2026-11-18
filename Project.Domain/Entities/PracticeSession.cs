namespace Project.Domain.Entities;

/// <summary>
/// Phiên luyện tập tự tạo của học viên (To-be 3.2). Tách khỏi ExamAttempts: không gắn lịch thi, không deadline,
/// không vào báo cáo Exam. SnapshotJson là AttemptSnapshot v2 (điểm mỗi câu = 1, có lời giải) — chỉ server đọc.
/// Kind: "practice" (chừa "mock" cho 3.3). RevealMode: "end" | "perQuestion". State: "inProgress" | "finished".
/// </summary>
public class PracticeSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int SubjectId { get; set; }
    public string Kind { get; set; } = "practice";
    public string RevealMode { get; set; } = "end";
    public string State { get; set; } = "inProgress";
    public string CriteriaJson { get; set; } = "{}";
    public string SnapshotJson { get; set; } = null!;
    public string DraftJson { get; set; } = "[]";
    public int Revision { get; set; }
    public int QuestionCount { get; set; }
    public int? CorrectCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public DateTime? DeadlineUtc { get; set; }
}
