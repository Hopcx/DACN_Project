namespace Project.Domain.Entities;

/// <summary>
/// To-be T18 (Q10, QT18-1/2): đoạn dùng chung (bài đọc/nghe) cho 2–10 câu con cùng môn. Câu con luôn đi cùng cả nhóm
/// (chọn tay lẫn ngẫu nhiên). Có chữ + media (QuestionGroupMedia, cùng giới hạn T14). Câu con đã vào đề → khóa sửa nhóm.
/// </summary>
public class QuestionGroup
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public byte Status { get; set; } = 1;          // 1 hoạt động, 0 ẩn (không được chọn vào đề/luyện tập mới)
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Subject Subject { get; set; } = null!;
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    public virtual ICollection<QuestionGroupMedia> Media { get; set; } = new List<QuestionGroupMedia>();
}

/// <summary>To-be T18: media của đoạn dùng chung, có thứ tự (≤ Media:MaxPerQuestion, ≤ MaxAudioPerQuestion audio).</summary>
public class QuestionGroupMedia
{
    public int GroupId { get; set; }
    public Guid MediaId { get; set; }
    public int SortOrder { get; set; }

    public virtual QuestionGroup Group { get; set; } = null!;
    public virtual MediaFile Media { get; set; } = null!;
}
