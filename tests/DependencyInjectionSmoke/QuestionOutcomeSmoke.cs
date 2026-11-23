using System.Text.Json;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

// To-be T11: snapshot v3 (chương/tag), đúng/sai từng câu khớp AttemptScoring, xếp loại theo ngưỡng.
internal static class QuestionOutcomeSmoke
{
    public static void Run()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        // BuildQuestion chụp chương + mọi tag đang gắn (kể cả tag ẩn), sắp xếp, bỏ trùng
        var question = new Question { Id = 7, QuestionTypeId = 2, Content = "Q", Status = 1, ChapterId = 3 };
        question.Answers.Add(new Answer { Id = 70, QuestionId = 7, Content = "A", IsCorrect = true, Status = 1 });
        question.Answers.Add(new Answer { Id = 71, QuestionId = 7, Content = "B", IsCorrect = false, Status = 1 });
        question.QuestionTags.Add(new QuestionTag { QuestionId = 7, TagId = 9 });
        question.QuestionTags.Add(new QuestionTag { QuestionId = 7, TagId = 4 });
        var built = AttemptSnapshotFactory.BuildQuestion(question, 1);
        Check(built.ChapterId == 3 && built.TagIds!.SequenceEqual(new[] { 4, 9 }), "v3 captures chapter and sorted tags.");
        var v3 = new AttemptSnapshot(AttemptSnapshot.CurrentVersion, 1, 1, "E", "V", new[] { built });
        var round = JsonSerializer.Deserialize<AttemptSnapshot>(JsonSerializer.Serialize(v3, json), json)!;
        Check(round.Version == AttemptSnapshot.CurrentVersion && round.Questions[0].ChapterId == 3 && round.Questions[0].TagIds!.SequenceEqual(new[] { 4, 9 }),
            "v3 JSON round-trips.");
        var v2Json = """{"version":2,"examId":1,"examDetailId":1,"examName":"E","variantCode":"V","questions":[{"id":"00000000-0000-0000-0000-000000000001","sourceQuestionId":7,"questionTypeId":2,"content":"Q","point":1,"options":[],"explanation":null}]}""";
        var v2 = JsonSerializer.Deserialize<AttemptSnapshot>(v2Json, json)!;
        Check(v2.Version == 2 && v2.Questions[0].ChapterId == null && v2.Questions[0].TagIds == null, "v2 JSON still reads (no chapter/tags).");

        // Ba câu: đúng / sai do chọn thừa / bỏ trống. Câu đa đáp án đúng.
        Guid G() => Guid.NewGuid();
        AttemptOptionSnapshot O(int src, bool ok) => new(G(), src, $"o{src}", ok);
        var q1 = new AttemptQuestionSnapshot(G(), 1, 2, "q1", 1, new[] { O(10, true), O(11, false) }, null, 5, new[] { 1 });
        var q2 = new AttemptQuestionSnapshot(G(), 2, 3, "q2", 2, new[] { O(20, true), O(21, true), O(22, false) }, null, 5, new[] { 1, 2 });
        var q3 = new AttemptQuestionSnapshot(G(), 3, 2, "q3", 1, new[] { O(30, true), O(31, false) }, null, null, Array.Empty<int>());
        var snap = new AttemptSnapshot(3, 1, 1, "E", "V", new[] { q1, q2, q3 });
        var answers = new List<AttemptAnswerDto>
        {
            new(q1.Id, new[] { q1.Options[0].Id }),
            new(q2.Id, new[] { q2.Options[0].Id, q2.Options[1].Id, q2.Options[2].Id }),
            new(q3.Id, Array.Empty<Guid>()),
        };
        var r = QuestionOutcomeRules.Evaluate(snap, answers);
        Check(r.Count == 3 && r[0].IsCorrect && r[0].IsAnswered && !r[1].IsCorrect && r[1].IsAnswered && !r[2].IsCorrect && !r[2].IsAnswered,
            "Correct / extra option wrong / blank wrong (QT11-2).");
        Check(r[1].SelectedSourceAnswerIds.SequenceEqual(new[] { 20, 21, 22 }) && r[2].SelectedSourceAnswerIds.Count == 0,
            "Selected answers stored as source answer ids.");
        Check(r.All(x => x.FromSnapshot) && r[1].ChapterId == 5 && r[1].TagIds.SequenceEqual(new[] { 1, 2 }) && r[2].ChapterId == null,
            "v3 classification from snapshot.");
        var score = AttemptScoring.Calculate(snap, answers, 0);
        Check(score.RawTotal == r.Where(x => x.IsCorrect).Sum(x => (decimal)snap.Questions.First(q => q.Id == x.SnapshotQuestionId).Point),
            "Outcomes agree with AttemptScoring.");
        var old = snap with { Version = 2 };
        var current = new Dictionary<int, (int?, IReadOnlyList<int>)> { [1] = (8, new[] { 3 }) };
        var ro = QuestionOutcomeRules.Evaluate(old, answers, current);
        Check(!ro[0].FromSnapshot && ro[0].ChapterId == 8 && ro[0].TagIds.SequenceEqual(new[] { 3 }) && ro[1].ChapterId == null && ro[1].TagIds.Count == 0,
            "v1/v2 use current classification; unknown question has none.");

        // Xếp loại: biên 50/70/85, dưới 5 câu chưa đủ
        var t = new AnalysisThresholds().Validate();
        Check(AnalysisRules.Classify(4, 4, t) == AnalysisRules.Insufficient && AnalysisRules.Classify(2, 5, t) == AnalysisRules.Weak &&
            AnalysisRules.Classify(5, 10, t) == AnalysisRules.NeedsImprovement && AnalysisRules.Classify(69, 100, t) == AnalysisRules.NeedsImprovement &&
            AnalysisRules.Classify(7, 10, t) == AnalysisRules.Fair && AnalysisRules.Classify(84, 100, t) == AnalysisRules.Fair &&
            AnalysisRules.Classify(17, 20, t) == AnalysisRules.Good && AnalysisRules.Classify(5, 5, t) == AnalysisRules.Good, "Classification boundaries.");
        Check(AnalysisRules.Classify(499, 1000, t) == AnalysisRules.Weak, "Unrounded rate decides (49.9% is Weak).");
        Check(AnalysisRules.Rate(1, 3) == 33.3m && AnalysisRules.Rate(0, 0) == null, "Rate rounding.");
        var bad = 0;
        foreach (var cfg in new[] { new AnalysisThresholds(70, 50, 85), new AnalysisThresholds(50, 70, 101), new AnalysisThresholds(0, 70, 85),
                     new AnalysisThresholds(50, 70, 85, 0) })
            try { cfg.Validate(); } catch (InvalidOperationException) { bad++; }
        Check(bad == 4, "Invalid thresholds rejected.");
        Console.WriteLine("QUESTION_OUTCOME_SNAPSHOT_V3_SCORING_CLASSIFY=PASS (isolated; no SQL/API)");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
