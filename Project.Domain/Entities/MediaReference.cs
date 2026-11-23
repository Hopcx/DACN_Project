namespace Project.Domain.Entities;

/// <summary>
/// To-be T15 (Q11c): media được snapshot của một lượt thi ("exam", SourceId = ExamAttempts.Id) hoặc phiên luyện tập/thi thử
/// ("practice", SourceId = PracticeSessions.Id) tham chiếu. Ghi cùng transaction lúc chụp snapshot; dùng để đếm tham chiếu
/// (media còn được dùng thì không xóa được) bằng SQL, không quét JSON.
/// </summary>
public class MediaReference
{
    public Guid MediaId { get; set; }
    public string SourceKind { get; set; } = null!;
    public Guid SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
