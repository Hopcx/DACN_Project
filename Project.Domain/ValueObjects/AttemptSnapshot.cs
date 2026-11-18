namespace Project.Domain.ValueObjects;

// Server-side snapshot only. Never return this type from a student endpoint.
// Points retain the current source representation; grading/rounding is a separate policy.
// Version 1: no explanation. Version 2 (To-be T4): AttemptQuestionSnapshot.Explanation captured at start.
public sealed record AttemptSnapshot(int Version, int ExamId, int ExamDetailId, string ExamName,
    string VariantCode, IReadOnlyList<AttemptQuestionSnapshot> Questions)
{
    public const int CurrentVersion = 2;
}

// Explanation is optional and last so v1 JSON (no field) deserializes with Explanation = null.
public sealed record AttemptQuestionSnapshot(Guid Id, int SourceQuestionId, int QuestionTypeId,
    string Content, double Point, IReadOnlyList<AttemptOptionSnapshot> Options, string? Explanation = null);

public sealed record AttemptOptionSnapshot(Guid Id, int SourceAnswerId, string Content, bool IsCorrect);
