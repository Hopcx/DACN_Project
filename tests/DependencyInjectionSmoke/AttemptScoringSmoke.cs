using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.ValueObjects;

internal static class AttemptScoringSmoke
{
    private static readonly Guid SingleId = Guid.NewGuid(), TrueFalseId = Guid.NewGuid(), MultiId = Guid.NewGuid();
    private static readonly Guid SingleGood = Guid.NewGuid(), SingleBad = Guid.NewGuid();
    private static readonly Guid TrueGood = Guid.NewGuid(), TrueBad = Guid.NewGuid();
    private static readonly Guid MultiGoodA = Guid.NewGuid(), MultiGoodB = Guid.NewGuid(), MultiBad = Guid.NewGuid();
    private static readonly AttemptSnapshot Snapshot = new(1, 1, 1, "Score QA", "QA", new[] {
        new AttemptQuestionSnapshot(SingleId, 1, 2, "Single", 1.005, new[] {
            new AttemptOptionSnapshot(SingleGood, 1, "A", true), new AttemptOptionSnapshot(SingleBad, 2, "B", false) }),
        new AttemptQuestionSnapshot(TrueFalseId, 2, 1, "True/false", 1.015, new[] {
            new AttemptOptionSnapshot(TrueGood, 3, "True", true), new AttemptOptionSnapshot(TrueBad, 4, "False", false) }),
        new AttemptQuestionSnapshot(MultiId, 3, 3, "Multi", 2.5, new[] {
            new AttemptOptionSnapshot(MultiGoodA, 5, "C", true), new AttemptOptionSnapshot(MultiGoodB, 6, "D", true),
            new AttemptOptionSnapshot(MultiBad, 7, "E", false) }) });

    private static AttemptAnswerDto Choice(Guid question, params Guid[] options) => new(question, options);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        var blank = AttemptScoring.Calculate(Snapshot, Array.Empty<AttemptAnswerDto>(), 0.001);
        Check(blank.RawTotal == 0 && blank.StoredTotal == 0 && !blank.IsPassed, "Blank must score zero.");
        var wrong = AttemptScoring.Calculate(Snapshot, new[] { Choice(SingleId, SingleBad) }, 0);
        Check(wrong.RawTotal == 0 && wrong.IsPassed, "Wrong single answer must score zero; zero pass mark passes.");
        var partial = AttemptScoring.Calculate(Snapshot, new[] { Choice(MultiId, MultiGoodA) }, 1);
        Check(partial.RawTotal == 0 && !partial.IsPassed, "Partial multi choice must score zero.");
        var extra = AttemptScoring.Calculate(Snapshot, new[] { Choice(MultiId, MultiGoodA, MultiGoodB, MultiBad) }, 1);
        Check(extra.RawTotal == 0, "Extra multi choice must score zero.");
        var exact = AttemptScoring.Calculate(Snapshot, new[] { Choice(MultiId, MultiGoodB, MultiGoodA) }, 2.5);
        Check(exact.RawTotal == 2.5m && exact.IsPassed, "Exact multi set must score full points regardless of order.");
        var lowerTie = AttemptScoring.Calculate(Snapshot, new[] { Choice(SingleId, SingleGood) }, 1.004);
        Check(lowerTie.RawTotal == 1.005m && lowerTie.StoredTotal == 1.00m && lowerTie.IsPassed,
            "Midpoint-to-even and pre-round pass threshold diverged.");
        var upperTie = AttemptScoring.Calculate(Snapshot, new[] { Choice(TrueFalseId, TrueGood) }, 1.016);
        Check(upperTie.RawTotal == 1.015m && upperTie.StoredTotal == 1.02m && !upperTie.IsPassed,
            "Rounded score must not change pass/fail decision.");
        var total = AttemptScoring.Calculate(Snapshot, new[] { Choice(SingleId, SingleGood),
            Choice(TrueFalseId, TrueGood), Choice(MultiId, MultiGoodA, MultiGoodB) }, 4.52);
        Check(total.RawTotal == 4.520m && total.StoredTotal == 4.52m && total.IsPassed,
            "Total must be summed before rounding once.");
        Console.WriteLine("ATTEMPT_SCORING_BLANK_SINGLE_MULTI_PARTIAL_EXTRA_ROUNDING_PASS_BOUNDARIES=PASS (isolated)");
    }
}
