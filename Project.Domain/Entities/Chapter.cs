namespace Project.Domain.Entities;

/// <summary>Chương của một môn (To-be 3.1, Q1: mỗi chương thuộc đúng một môn). Status: 1 hoạt động, 0 tạm ngừng, 255 đã ẩn.</summary>
public class Chapter
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public byte Status { get; set; } = 1;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Subject Subject { get; set; } = null!;
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
