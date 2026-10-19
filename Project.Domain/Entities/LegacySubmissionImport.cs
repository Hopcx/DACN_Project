namespace Project.Domain.Entities;

// Isolated historical projection. Source timestamps have no verified offset.
public sealed class LegacyImportBatch
{
    public Guid Id { get; set; }
    public string SourceSystem { get; set; } = null!;
    public string SourceDatabase { get; set; } = null!;
    public string SourceMigration { get; set; } = null!;
    public string Marker { get; set; } = null!;
    public DateTime ImportedAtUtc { get; set; }
}

public sealed class LegacyUserMap
{
    public long Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid SourceUserId { get; set; }
    public Guid TargetUserId { get; set; }
}

public sealed class LegacySubmissionRecord
{
    public long Id { get; set; }
    public Guid BatchId { get; set; }
    public string SourceTable { get; set; } = "Submissions";
    public int SourceId { get; set; }
    public Guid SourceUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public int SourceExamId { get; set; }
    public int SourceExamDetailId { get; set; }
    public int SourceScheduleId { get; set; }
    public string ExamName { get; set; } = null!;
    public string? ScheduleTitle { get; set; }
    public DateTime SubmitTimeUnknown { get; set; }
    public TimeOnly TimeTaken { get; set; }
    public double TotalMark { get; set; }
    public bool IsPassed { get; set; }
    public bool? LegacyStatus { get; set; }
    public bool AllowViewResult { get; set; }
    public int Answered { get; set; }
    public int UnAnswered { get; set; }
    public byte[] SourceChecksum { get; set; } = null!;
    public LegacyImportBatch Batch { get; set; } = null!;
}

public sealed class LegacyAnswerSelection
{
    public long Id { get; set; }
    public long LegacySubmissionRecordId { get; set; }
    public string SourceTable { get; set; } = "AnswerSubmissions";
    public int SourceId { get; set; }
    public int SourceQuestionId { get; set; }
    public int SourceAnswerId { get; set; }
    public byte[] SourceChecksum { get; set; } = null!;
}
