using Project.Application.Services;
using Project.Domain.ValueObjects;

// To-be T8: quy tắc thuần của đánh dấu câu.
internal static class ReviewFlagSmoke
{
    public static void Run()
    {
        Check(ReviewFlagRules.NormalizeKind(" Exam ") == "exam" && ReviewFlagRules.NormalizeKind("PRACTICE") == "practice", "Kind normalized.");
        Check(ReviewFlagRules.NormalizeKind("mock") == null && ReviewFlagRules.NormalizeKind(null) == null && ReviewFlagRules.NormalizeKind("") == null, "Invalid kind rejected.");
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var options = Array.Empty<AttemptOptionSnapshot>();
        var snapshot = new AttemptSnapshot(2, 0, 0, "Luyện tập", "PRACTICE", new[]
        {
            new AttemptQuestionSnapshot(a, 1, 2, "Q1", 1, options), new AttemptQuestionSnapshot(b, 2, 2, "Q2", 1, options),
            new AttemptQuestionSnapshot(c, 3, 2, "Q3", 1, options),
        });
        Check(ReviewFlagRules.HasQuestion(snapshot, b) && !ReviewFlagRules.HasQuestion(snapshot, Guid.NewGuid()) &&
            !ReviewFlagRules.HasQuestion(snapshot, Guid.Empty), "Question must belong to the snapshot.");
        Check(ReviewFlagRules.InSnapshotOrder(snapshot, new[] { c, Guid.NewGuid(), a }).SequenceEqual(new[] { a, c }),
            "Flags listed in snapshot order, unknown ids dropped.");
        Console.WriteLine("REVIEW_FLAG_KIND_MEMBERSHIP_ORDER=PASS (isolated; no SQL/API)");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
