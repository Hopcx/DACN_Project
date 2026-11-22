namespace Project.Application.Services;

/// <summary>Kết quả một câu trong lượt được tính của một học viên (To-be T12).</summary>
public sealed record ReportOutcome(Guid UserId, int SourceQuestionId, int? ChapterId, bool IsCorrect, IReadOnlyList<int> SelectedAnswerIds);

public sealed record ReportChapter(int? ChapterId, int Total, int Correct, decimal? Rate, int StudentCount, IReadOnlyList<Guid> WeakStudents);

public sealed record ReportHardQuestion(int SourceQuestionId, int Answers, int Correct, decimal? Rate, int? DistractorAnswerId, int DistractorCount,
    int? ChapterId = null);

public sealed record ClassReportResult(IReadOnlyList<ReportChapter> Chapters, IReadOnlyList<ReportHardQuestion> HardQuestions,
    int MaxAnswersPerQuestion, int QuestionCount);

/// <summary>
/// To-be T12 (Q13b): báo cáo lớp từ kết quả từng câu của các lượt được tính (QT12-1). Hàm thuần, không I/O.
/// - Theo chương: tỉ lệ đúng của lớp; học viên "Yếu" ở chương khi có ≥ MinQuestions câu và xếp loại Yếu (ngưỡng T11).
/// - Câu hay sai: tỉ lệ đúng (chưa làm tròn) &lt; HardBelow với ≥ HardMinAnswers lượt; đáp án nhiễu = đáp án sai được chọn
///   nhiều nhất trong các lượt sai (hòa: Id nhỏ hơn). Sắp tỉ lệ tăng dần, nhiều lượt trước; tối đa <see cref="MaxHardQuestions"/>.
/// <paramref name="correctAnswerIds"/>: Id đáp án đúng theo snapshot lúc thi (để loại khỏi đáp án nhiễu); không bao giờ trả ra ngoài.
/// "Lượt" gồm cả câu bỏ trống (tính là sai, như điểm).
/// </summary>
public static class ClassReport
{
    public const int MaxHardQuestions = 50;

    public static ClassReportResult Build(IReadOnlyList<ReportOutcome> outcomes, AnalysisThresholds t, IReadOnlySet<int> correctAnswerIds)
    {
        var chapters = outcomes.GroupBy(o => o.ChapterId)
            .Select(g =>
            {
                var weak = g.GroupBy(o => o.UserId)
                    .Where(u => AnalysisRules.Classify(u.Count(o => o.IsCorrect), u.Count(), t) == AnalysisRules.Weak)
                    .Select(u => u.Key).OrderBy(x => x).ToList();
                var correct = g.Count(o => o.IsCorrect);
                return new ReportChapter(g.Key, g.Count(), correct, AnalysisRules.Rate(correct, g.Count()),
                    g.Select(o => o.UserId).Distinct().Count(), weak);
            })
            .OrderBy(x => x.ChapterId == null).ThenBy(x => x.ChapterId).ToList();

        var byQuestion = outcomes.GroupBy(o => o.SourceQuestionId).ToList();
        var hard = byQuestion
            .Select(g => new { g.Key, Answers = g.Count(), Correct = g.Count(o => o.IsCorrect), Items = g })
            .Where(x => x.Answers >= t.HardMinAnswers && 100m * x.Correct / x.Answers < t.HardBelow)
            .Select(x =>
            {
                var distractor = x.Items.Where(o => !o.IsCorrect).SelectMany(o => o.SelectedAnswerIds.Distinct())
                    .Where(id => !correctAnswerIds.Contains(id))
                    .GroupBy(id => id).OrderByDescending(d => d.Count()).ThenBy(d => d.Key).FirstOrDefault();
                // Review T12: chương của câu theo kết quả (snapshot) — chương xuất hiện nhiều nhất, hòa thì Id nhỏ — khớp bảng "Theo chương"
                var chapter = x.Items.GroupBy(o => o.ChapterId).OrderByDescending(c => c.Count()).ThenBy(c => c.Key ?? int.MaxValue).First().Key;
                return new ReportHardQuestion(x.Key, x.Answers, x.Correct, AnalysisRules.Rate(x.Correct, x.Answers),
                    distractor?.Key, distractor?.Count() ?? 0, chapter);
            })
            .OrderBy(x => (decimal)x.Correct / x.Answers).ThenByDescending(x => x.Answers).ThenBy(x => x.SourceQuestionId)
            .Take(MaxHardQuestions).ToList();
        return new ClassReportResult(chapters, hard, byQuestion.Count == 0 ? 0 : byQuestion.Max(g => g.Count()), byQuestion.Count);
    }
}
