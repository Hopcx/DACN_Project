namespace Project.Domain.Entities;

/// <summary>Tag phân loại câu hỏi (To-be 3.1, Q2). NormalizedName (chữ thường, bỏ dấu) là khóa duy nhất để tránh tag trùng.</summary>
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;
    public byte Status { get; set; } = 1;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<QuestionTag> QuestionTags { get; set; } = new List<QuestionTag>();
}

/// <summary>Liên kết nhiều-nhiều câu hỏi – tag.</summary>
public class QuestionTag
{
    public int QuestionId { get; set; }
    public int TagId { get; set; }

    public virtual Question Question { get; set; } = null!;
    public virtual Tag Tag { get; set; } = null!;
}
