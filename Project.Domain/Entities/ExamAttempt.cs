namespace Project.Domain.Entities;

// New attempts are separate from legacy DoingExam rows whose timezone and linkage are unknown.
public class ExamAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int ExamScheduleId { get; set; }
    public int ExamId { get; set; }
    public int ExamDetailId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string State { get; set; } = "inProgress";
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string DraftJson { get; set; } = "[]";
    public double PassMark { get; set; }
    public bool AllowViewResult { get; set; }
    public ExamAttemptSubmission? Submission { get; set; }
}

public class ExamAttemptSubmission
{
    public Guid ExamAttemptId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public long? TimeTakenSeconds { get; set; }
    public decimal TotalMark { get; set; }
    public bool IsPassed { get; set; }
    public string AnswersJson { get; set; } = null!;
    public ExamAttempt Attempt { get; set; } = null!;
}
