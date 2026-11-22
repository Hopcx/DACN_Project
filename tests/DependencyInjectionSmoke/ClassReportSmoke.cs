using Project.Api.Services;
using Project.Application.Services;

// To-be T12: lượt được tính, báo cáo theo chương, câu hay sai + đáp án nhiễu.
internal static class ClassReportSmoke
{
    public static void Run()
    {
        // ChooseAttempts: thang 10 cao nhất; hòa thì nộp sau; bỏ lượt tổng 0
        var u = Guid.NewGuid(); var a1 = Guid.NewGuid(); var a2 = Guid.NewGuid(); var a3 = Guid.NewGuid(); var a4 = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var chosen = ClassRanking.ChooseAttempts(new[]
        {
            new RankingAttempt(u, 1, 8, 10, true, a1, t0), new RankingAttempt(u, 1, 16, 20, true, a2, t0.AddMinutes(5)),
            new RankingAttempt(u, 1, 5, 10, false, a3, t0.AddMinutes(9)), new RankingAttempt(u, 2, 9, 0, true, a4, t0),
        });
        Check(chosen.Count == 1 && chosen[(u, 1)] == a2, "Best ten-scale attempt; tie → later submission; zero max skipped.");

        // Theo chương: 6 học viên; chương 1 có 5 câu mỗi người; học viên s0, s1 "Yếu" (< 50%), s5 chỉ 4 câu → chưa xếp loại
        var t = new AnalysisThresholds().Validate();
        var s = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var outcomes = new List<ReportOutcome>();
        void Add(Guid user, int q, int? ch, bool ok, params int[] sel) => outcomes.Add(new ReportOutcome(user, q, ch, ok, sel));
        int[] correctByUser = { 2, 1, 3, 4, 5 };
        for (var i = 0; i < 5; i++)
            for (var q = 1; q <= 5; q++) Add(s[i], q, 1, q <= correctByUser[i], q <= correctByUser[i] ? 101 : 102);
        for (var q = 1; q <= 4; q++) Add(s[5], q, 1, false, 102);
        Add(s[0], 9, null, true, 901);
        var r1 = ClassReport.Build(outcomes, t, new HashSet<int> { 101, 901 });
        var c1 = r1.Chapters.Single(c => c.ChapterId == 1);
        Check(c1.Total == 29 && c1.Correct == 15 && c1.StudentCount == 6 && c1.Rate == 51.7m, "Chapter totals and rate.");
        Check(c1.WeakStudents.OrderBy(x => x).SequenceEqual(new[] { s[0], s[1] }.OrderBy(x => x)), "Weak only with ≥ 5 câu and < 50%.");
        Check(r1.Chapters.Last().ChapterId == null && r1.Chapters.Last().Total == 1, "Unchaptered row last.");

        // Câu hay sai: q1..q5 mỗi câu 6 lượt (< 10) → không có; ngưỡng 10
        Check(r1.HardQuestions.Count == 0 && r1.MaxAnswersPerQuestion == 6 && r1.QuestionCount == 6, "Below 10 answers: none.");
        // 10 lượt: câu A 3/10 = 30% (vào), câu B 4/10 = 40% (biên, không vào), câu C 9 lượt 0% (không vào)
        var hard = new List<ReportOutcome>();
        for (var i = 0; i < 10; i++)
        {
            var user = Guid.NewGuid();
            hard.Add(new ReportOutcome(user, 50, 1, i < 3, i < 3 ? new[] { 501 } : i < 8 ? new[] { 502 } : new[] { 503, 501 }));
            hard.Add(new ReportOutcome(user, 60, 1, i < 4, i < 4 ? new[] { 601 } : new[] { 602 }));
            if (i < 9) hard.Add(new ReportOutcome(user, 70, 1, false, new[] { 702 }));
        }
        var r2 = ClassReport.Build(hard, t, new HashSet<int> { 501, 601, 701 });
        Check(r2.HardQuestions.Count == 1 && r2.HardQuestions[0].SourceQuestionId == 50 && r2.HardQuestions[0].Rate == 30m,
            "Hard: < 40% with ≥ 10 answers; 40% boundary and 9 answers excluded.");
        Check(r2.HardQuestions[0].DistractorAnswerId == 502 && r2.HardQuestions[0].DistractorCount == 5,
            "Distractor = most chosen wrong answer among wrong outcomes (correct option in multi-select ignored).");

        // Review T12: chương của câu = chương xuất hiện nhiều nhất trong kết quả; hòa số lần chọn đáp án nhiễu → Id nhỏ hơn
        var tie = new List<ReportOutcome>();
        for (var i = 0; i < 10; i++)
            tie.Add(new ReportOutcome(Guid.NewGuid(), 80, i < 7 ? 3 : 4, false, new[] { i % 2 == 0 ? 803 : 802 }));
        var r3 = ClassReport.Build(tie, t, new HashSet<int> { 801 });
        Check(r3.HardQuestions.Single().ChapterId == 3 && r3.HardQuestions[0].DistractorAnswerId == 802 && r3.HardQuestions[0].DistractorCount == 5,
            "Question chapter = most frequent outcome chapter; distractor tie → smaller id.");
        var emoji = new string('a', 198) + "😀b";
        Check(!char.IsHighSurrogate(ClassReportQuery.Shorten(emoji)[^2]) && ClassReportQuery.Shorten(emoji).EndsWith('…'), "Shorten never splits a surrogate pair.");

        Check(ClassReportQuery.Shorten(new string('a', 250)).Length == 200 && ClassReportQuery.Shorten("x\r\ny") == "x y", "Content shortened to 200.");
        var bad = 0;
        foreach (var cfg in new[] { new AnalysisThresholds(HardBelow: 0), new AnalysisThresholds(HardBelow: 101), new AnalysisThresholds(HardMinAnswers: 0) })
            try { cfg.Validate(); } catch (InvalidOperationException) { bad++; }
        Check(bad == 3, "Invalid hard thresholds rejected.");
        Console.WriteLine("CLASS_REPORT_CHOSEN_CHAPTER_WEAK_HARD_DISTRACTOR=PASS (isolated; no SQL/API)");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
