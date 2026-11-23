namespace Project.Domain.Entities;

/// <summary>
/// To-be T14 (Q11a): một file media (ảnh/âm thanh) lưu trên đĩa ngoài wwwroot, tên file = Id. Loại xác định từ
/// magic bytes (không tin đuôi/MIME client). Trùng SHA-256 thì dùng lại (QT14-2). Không xóa tự động (Q15).
/// </summary>
public class MediaFile
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = null!;          // "image" | "audio"
    public string ContentType { get; set; } = null!;   // theo loại đã phát hiện
    public string Extension { get; set; } = null!;     // ".png", ".jpg", ".webp", ".mp3", ".m4a", ".ogg"
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = null!;        // hex chữ thường, 64 ký tự
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string OriginalName { get; set; } = null!;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public byte Status { get; set; } = 1;

    public virtual ICollection<QuestionMedia> QuestionMedia { get; set; } = new List<QuestionMedia>();
}

/// <summary>To-be T14 (QT14-3): đính kèm media của câu hỏi (≤ 5, ≤ 1 audio), có thứ tự.</summary>
public class QuestionMedia
{
    public int QuestionId { get; set; }
    public Guid MediaId { get; set; }
    public int SortOrder { get; set; }

    public virtual Question Question { get; set; } = null!;
    public virtual MediaFile Media { get; set; } = null!;
}
