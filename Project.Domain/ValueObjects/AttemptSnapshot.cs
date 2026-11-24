namespace Project.Domain.ValueObjects;

// Server-side snapshot only. Never return this type from a student endpoint.
// Points retain the current source representation; grading/rounding is a separate policy.
// Version 1: no explanation. Version 2 (To-be T4): AttemptQuestionSnapshot.Explanation captured at start.
// Version 3 (To-be T11, Q13d): ChapterId and TagIds of each question captured at start (classification for analysis).
// Version 4 (To-be T15, Q11c): Media (id, kind, size) of each question captured at start; student URLs are signed per request.
// Version 5 (To-be T16, Q10a): short-answer questions (type 4) carry ShortAnswer rules; their accepted answers are the
// correct Options (never sent to students before review).
// Version 6 (To-be T17): matching (type 5) options carry MatchId + MatchContent (right side, own Guid) and the question carries
// MatchOrder (right sides shuffled once at capture); multi-statement (type 6) options are statements, IsCorrect = statement is true.
public sealed record AttemptSnapshot(int Version, int ExamId, int ExamDetailId, string ExamName,
    string VariantCode, IReadOnlyList<AttemptQuestionSnapshot> Questions)
{
    public const int CurrentVersion = 6;
}

// Optional fields are last so older JSON (v1 without Explanation, v1/v2 without ChapterId/TagIds) still deserializes.
public sealed record AttemptQuestionSnapshot(Guid Id, int SourceQuestionId, int QuestionTypeId,
    string Content, double Point, IReadOnlyList<AttemptOptionSnapshot> Options, string? Explanation = null,
    int? ChapterId = null, IReadOnlyList<int>? TagIds = null, IReadOnlyList<AttemptMediaSnapshot>? Media = null,
    AttemptShortAnswerSnapshot? ShortAnswer = null, IReadOnlyList<Guid>? MatchOrder = null);

public sealed record AttemptMediaSnapshot(Guid Id, string Kind, int? Width, int? Height);

// To-be T16: so khớp câu điền ngắn — bỏ dấu tiếng Việt; so số với sai số tuyệt đối (Tolerance ≥ 0, chỉ dùng khi Numeric).
public sealed record AttemptShortAnswerSnapshot(bool IgnoreDiacritics, bool Numeric, decimal Tolerance);

public sealed record AttemptOptionSnapshot(Guid Id, int SourceAnswerId, string Content, bool IsCorrect,
    Guid? MatchId = null, string? MatchContent = null);
