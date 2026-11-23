namespace Project.Domain.Entities;

/// <summary>
/// To-be T11 (Q13): kết quả từng câu của một lượt thi ("exam", SourceId = ExamAttempts.Id) hoặc phiên luyện tập/thi thử
/// ("practice"/"mock", SourceId = PracticeSessions.Id) đã chốt. Ghi cùng transaction lúc chốt; dữ liệu cũ do worker bù.
/// Đúng/sai theo cùng quy tắc tất-cả-hoặc-không với điểm. Không FK tới nguồn (đa hình); lượt bị hủy lọc qua submission.
/// ClassificationSource: "snapshot" (chương/tag chụp lúc làm, snapshot v3) hoặc "current" (v1/v2, theo ngân hàng lúc bù).
/// </summary>
public class QuestionOutcome
{
    public string SourceKind { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Guid SnapshotQuestionId { get; set; }
    public Guid UserId { get; set; }
    public int SubjectId { get; set; }
    public int? ExamScheduleId { get; set; }
    public int SourceQuestionId { get; set; }
    public int? ChapterId { get; set; }
    public string TagIdsJson { get; set; } = "[]";
    public bool IsCorrect { get; set; }
    public bool IsAnswered { get; set; }
    public string SelectedAnswerIdsJson { get; set; } = "[]";
    // To-be T16 (QT16-4): bài làm câu điền ngắn đã chuẩn hóa (≤ 200 ký tự); loại khác NULL
    public string? AnswerText { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public string ClassificationSource { get; set; } = "snapshot";
}
