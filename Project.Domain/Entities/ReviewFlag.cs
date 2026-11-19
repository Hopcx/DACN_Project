namespace Project.Domain.Entities;

/// <summary>
/// To-be T8 (3.4): đánh dấu câu để xem lại của học viên, dùng chung cho lượt thi (SourceKind "exam", SourceId = ExamAttempts.Id)
/// và phiên luyện tập ("practice", SourceId = PracticeSessions.Id). SnapshotQuestionId là Guid câu trong snapshot của nguồn.
/// Không FK tới nguồn (đa hình): API kiểm chủ sở hữu và câu thuộc snapshot. Chỉ là ghi chú riêng, không ảnh hưởng điểm.
/// </summary>
public class ReviewFlag
{
    public Guid UserId { get; set; }
    public string SourceKind { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Guid SnapshotQuestionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
