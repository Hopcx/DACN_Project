namespace Project.Domain.Entities;

public class AttemptVisibilityEvent
{
    public Guid AttemptId { get; set; }
    public Guid EventId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public bool IsCounted { get; set; }
    public ExamAttempt Attempt { get; set; } = null!;
}
