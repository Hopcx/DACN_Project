using Project.Application.DTOs.AttemptDTO;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

public sealed record AttemptScore(decimal RawTotal, decimal StoredTotal, bool IsPassed);

// The saved snapshot is the only source of answer keys and points at finalization.
public static class AttemptScoring
{
    public static void ValidateConfiguration(AttemptSnapshot snapshot, double passMark)
    {
        if (!double.IsFinite(passMark) || passMark < 0)
            throw new ArgumentException("Pass mark is invalid.", nameof(passMark));
        decimal maximum = 0;
        try
        {
            _ = Convert.ToDecimal(passMark);
            foreach (var question in snapshot.Questions)
            {
                if (!double.IsFinite(question.Point) || question.Point < 0)
                    throw new ArgumentException("Question point is invalid.", nameof(snapshot));
                maximum = checked(maximum + Convert.ToDecimal(question.Point));
            }
        }
        catch (OverflowException ex) { throw new ArgumentException("Score cannot be stored.", nameof(snapshot), ex); }
        // SQL decimal(18,2) permits 16 integer digits; reserve room for rounding.
        if (maximum >= 10000000000000000m)
            throw new ArgumentException("Score cannot be stored.", nameof(snapshot));
    }

    public static AttemptScore Calculate(AttemptSnapshot snapshot,
        IReadOnlyList<AttemptAnswerDto> savedAnswers, double passMark)
    {
        ValidateConfiguration(snapshot, passMark);
        var answers = AttemptSnapshotFactory.ValidateDraft(snapshot, savedAnswers)
            .ToDictionary(x => x.QuestionId);
        decimal raw = 0;
        foreach (var question in snapshot.Questions)
        {
            var selected = answers[question.Id].SelectedAnswerIds;
            if (selected.Count == 0) continue;
            var correct = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToHashSet();
            // All-or-nothing for every supported question type. Missing or extra options score zero.
            if (selected.ToHashSet().SetEquals(correct))
                raw = checked(raw + Convert.ToDecimal(question.Point));
        }
        // Testify uses Math.Round(score, 2) and compares before rounding.
        return new AttemptScore(raw, decimal.Round(raw, 2, MidpointRounding.ToEven),
            raw >= Convert.ToDecimal(passMark));
    }
}
