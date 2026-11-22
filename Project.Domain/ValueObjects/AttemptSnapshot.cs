namespace Project.Domain.ValueObjects;

// Server-side snapshot only. Never return this type from a student endpoint.
// Points retain the current source representation; grading/rounding is a separate policy.
// Version 1: no explanation. Version 2 (To-be T4): AttemptQuestionSnapshot.Explanation captured at start.
// Version 3 (To-be T11, Q13d): ChapterId and TagIds of each question captured at start (classification for analysis).
public sealed record AttemptSnapshot(int Version, int ExamId, int ExamDetailId, string ExamName,
    string VariantCode, IReadOnlyList<AttemptQuestionSnapshot> Questions)
{
    public const int CurrentVersion = 3;
}

// Optional fields are last so older JSON (v1 without Explanation, v1/v2 without ChapterId/TagIds) still deserializes.
public sealed record AttemptQuestionSnapshot(Guid Id, int SourceQuestionId, int QuestionTypeId,
    string Content, double Point, IReadOnlyList<AttemptOptionSnapshot> Options, string? Explanation = null,
    int? ChapterId = null, IReadOnlyList<int>? TagIds = null);

public sealed record AttemptOptionSnapshot(Guid Id, int SourceAnswerId, string Content, bool IsCorrect);
