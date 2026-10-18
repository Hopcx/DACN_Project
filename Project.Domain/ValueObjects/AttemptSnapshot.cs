namespace Project.Domain.ValueObjects;

// Server-side snapshot only. Never return this type from a student endpoint.
// Points retain the current source representation; grading/rounding is a separate policy.
public sealed record AttemptSnapshot(int Version, int ExamId, int ExamDetailId, string ExamName,
    string VariantCode, IReadOnlyList<AttemptQuestionSnapshot> Questions);

public sealed record AttemptQuestionSnapshot(Guid Id, int SourceQuestionId, int QuestionTypeId,
    string Content, double Point, IReadOnlyList<AttemptOptionSnapshot> Options);

public sealed record AttemptOptionSnapshot(Guid Id, int SourceAnswerId, string Content, bool IsCorrect);
