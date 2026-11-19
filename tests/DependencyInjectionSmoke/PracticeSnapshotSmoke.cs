using System.Text.Json;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;

// To-be T5: chuẩn hóa tiêu chí, chọn ngẫu nhiên không lặp, snapshot luyện tập (điểm 1, lời giải), DTO an toàn, chấm.
internal static class PracticeSnapshotSmoke
{
    public static void Run()
    {
        var (ok, error) = PracticeSnapshotFactory.Normalize(3, new[] { 5, 2, 5 }, null, new[] { 9 }, 10, null);
        Check(error == null && ok!.ChapterIds.SequenceEqual(new[] { 2, 5 }) && ok.QuestionLevelIds.Count == 0 &&
            ok.TagIds.SequenceEqual(new[] { 9 }) && ok.RevealMode == "end", "Criteria must be deduped, sorted, default 'end'.");
        Check(PracticeSnapshotFactory.Normalize(3, null, null, null, 1, " perQuestion ").Value?.RevealMode == "perQuestion", "perQuestion accepted.");
        foreach (var bad in new[]
        {
            PracticeSnapshotFactory.Normalize(0, null, null, null, 10, "end"),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 0, "end"),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 51, "end"),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, "all"),
            PracticeSnapshotFactory.Normalize(3, new[] { 0 }, null, null, 10, "end"),
            PracticeSnapshotFactory.Normalize(3, null, Enumerable.Range(1, 21), null, 10, "end"),
        }) Check(bad.Value == null && bad.Error != null, "Invalid criteria accepted.");

        var pool = Enumerable.Range(1, 30).ToArray();
        var picked = PracticeSnapshotFactory.Pick(pool, 10, new Random(7));
        Check(picked.Count == 10 && picked.Distinct().Count() == 10 && picked.All(pool.Contains), "Pick must return distinct items from the pool.");
        Check(PracticeSnapshotFactory.Pick(pool, 30, new Random(1)).Order().SequenceEqual(pool), "Pick all must be a permutation.");
        Check(pool.SequenceEqual(Enumerable.Range(1, 30)), "Pick must not mutate the pool.");
        Reject(() => PracticeSnapshotFactory.Pick(pool, 31, new Random(1)));

        var good = Make(1, 2, "  Vì A  ");
        var multi = Make(2, 3, null);
        var hidden = Make(3, 2, null); hidden.Status = 0;
        var otherSubject = Make(4, 2, null); otherSubject.SubjectId = 99;
        var badSet = Make(5, 2, null); badSet.Answers.First(x => !x.IsCorrect).IsCorrect = true; // hai đáp án đúng cho loại 2
        Check(PracticeSnapshotFactory.IsUsable(good, 1) && PracticeSnapshotFactory.IsUsable(multi, 1), "Valid questions must be usable.");
        Check(!PracticeSnapshotFactory.IsUsable(hidden, 1) && !PracticeSnapshotFactory.IsUsable(otherSubject, 1) &&
            !PracticeSnapshotFactory.IsUsable(badSet, 1), "Invalid questions must be excluded.");

        var snapshot = PracticeSnapshotFactory.Create(1, new[] { multi, good });
        Check(snapshot.Version == 2 && snapshot.ExamId == 0 && snapshot.ExamDetailId == 0 && snapshot.VariantCode == "PRACTICE",
            "Practice snapshot header.");
        Check(snapshot.Questions.Select(x => x.SourceQuestionId).SequenceEqual(new[] { 2, 1 }) &&
            snapshot.Questions.All(x => x.Point == 1), "Order kept and each question worth 1.");
        Check(snapshot.Questions[1].Explanation == "Vì A" && snapshot.Questions[0].Explanation == null, "Explanation captured.");
        Reject(() => PracticeSnapshotFactory.Create(1, new[] { good, hidden }));
        Reject(() => PracticeSnapshotFactory.Create(1, new[] { good, good }));
        Reject(() => PracticeSnapshotFactory.Create(1, Array.Empty<Question>()));

        var safe = JsonSerializer.Serialize(AttemptSnapshotFactory.ForStudent(snapshot));
        Check(!safe.Contains("IsCorrect") && !safe.Contains("Explanation") && !safe.Contains("Vì A") && !safe.Contains("Point"),
            "Practice DTO must not leak keys, points or explanation.");

        // Chấm: câu 1 (nhiều đáp án) chọn đủ → đúng; câu 2 chọn sai → 0. Tổng = số câu đúng.
        var q0 = snapshot.Questions[0]; var q1 = snapshot.Questions[1];
        var answers = new[]
        {
            new AttemptAnswerDto(q0.Id, q0.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToArray()),
            new AttemptAnswerDto(q1.Id, new[] { q1.Options.First(x => !x.IsCorrect).Id })
        };
        var score = AttemptScoring.Calculate(snapshot, answers, 0);
        Check(score.RawTotal == 1, $"Practice score must count correct questions, got {score.RawTotal}.");
        Console.WriteLine("PRACTICE_SNAPSHOT_CRITERIA_PICK_USABLE_SAFE_DTO_SCORING=PASS (isolated; no SQL/API)");

        // To-be T6: chốt từng câu, kết quả câu đã chốt, khóa lựa chọn khi lưu nháp
        var empty = AttemptSnapshotFactory.ValidateDraft(snapshot, Array.Empty<AttemptAnswerDto>());
        var right = q0.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToArray();
        var (d1, c1) = PracticeCheck.Apply(snapshot, empty, Array.Empty<Guid>(), q0.Id, right);
        Check(c1.SequenceEqual(new[] { q0.Id }) && d1.First(x => x.QuestionId == q0.Id).SelectedAnswerIds.ToHashSet().SetEquals(right),
            "Apply must store the selection and lock the question.");
        var r0 = PracticeCheck.Result(q0, d1.First(x => x.QuestionId == q0.Id).SelectedAnswerIds);
        Check(r0.IsCorrect && r0.CorrectAnswerIds.ToHashSet().SetEquals(right) && r0.Explanation == null, "Correct check result.");
        var wrong = new[] { q1.Options.First(x => !x.IsCorrect).Id };
        var (d2, c2) = PracticeCheck.Apply(snapshot, d1, c1, q1.Id, wrong);
        var r1 = PracticeCheck.Result(q1, d2.First(x => x.QuestionId == q1.Id).SelectedAnswerIds);
        Check(!r1.IsCorrect && r1.Explanation == "Vì A" && r1.CorrectAnswerIds.Count == 1, "Wrong check result with explanation.");
        Check(PracticeCheck.Results(snapshot, d2, c2).Select(x => x.QuestionId).SequenceEqual(new[] { q0.Id, q1.Id }) &&
            PracticeCheck.Results(snapshot, d1, c1).Count == 1, "Results only for checked questions, in snapshot order.");
        Reject(() => PracticeCheck.Apply(snapshot, d1, c1, q0.Id, right));                       // đã chốt
        Reject(() => PracticeCheck.Apply(snapshot, d1, c1, q1.Id, Array.Empty<Guid>()));          // trống
        Reject(() => PracticeCheck.Apply(snapshot, d1, c1, Guid.NewGuid(), wrong));                // không thuộc phiên
        Reject(() => PracticeCheck.Apply(snapshot, d1, c1, q1.Id, q1.Options.Select(x => x.Id).ToArray())); // loại 2 chọn 2

        // Merge: bỏ qua câu đã chốt → giữ; gửi lại y nguyên → được; đổi → khóa; câu chưa chốt thay toàn bộ như T5
        var kept = PracticeCheck.Merge(snapshot, d1, c1, Array.Empty<AttemptAnswerDto>());
        Check(kept.First(x => x.QuestionId == q0.Id).SelectedAnswerIds.ToHashSet().SetEquals(right), "Omitted checked question keeps its selection.");
        var same = PracticeCheck.Merge(snapshot, d1, c1, new[] { new AttemptAnswerDto(q0.Id, right.Reverse().ToArray()), new AttemptAnswerDto(q1.Id, wrong) });
        Check(same.First(x => x.QuestionId == q1.Id).SelectedAnswerIds.SequenceEqual(wrong), "Unchecked question saved normally.");
        var locked = false;
        try { PracticeCheck.Merge(snapshot, d1, c1, new[] { new AttemptAnswerDto(q0.Id, new[] { right[0] }) }); }
        catch (PracticeQuestionLockedException) { locked = true; }
        Check(locked, "Changing a checked question must be rejected.");
        Console.WriteLine("PRACTICE_CHECK_APPLY_RESULT_MERGE_LOCK=PASS (isolated; no SQL/API)");

        // To-be T9: Thi thử — tiêu chí, thời lượng, hạn nộp, ân hạn 5 giây, chốt theo nháp đã lưu
        var mock = PracticeSnapshotFactory.Normalize(3, null, null, null, 10, null, " MOCK ", 15).Value!;
        Check(mock.Kind == "mock" && mock.DurationMinutes == 15 && mock.RevealMode == "end", "Mock criteria normalized.");
        Check(PracticeSnapshotFactory.Normalize(3, null, null, null, 10, "end", "practice", 30).Value!.DurationMinutes == null,
            "Practice ignores duration.");
        foreach (var bad in new[]
        {
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, "perQuestion", "mock", 15),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, null, "mock", null),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, null, "mock", 4),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, null, "mock", 181),
            PracticeSnapshotFactory.Normalize(3, null, null, null, 10, null, "exam", 15),
        }) Check(bad.Value == null && bad.Error != null, "Invalid mock criteria accepted.");
        var created = new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc);
        var due = PracticeTiming.Deadline(created, 15);
        Check(due == created.AddMinutes(15), "Deadline = created + duration.");
        Check(!PracticeTiming.IsExpired(due, due.AddSeconds(4.9)) && PracticeTiming.IsExpired(due, due.AddSeconds(5)) &&
            !PracticeTiming.IsExpired(null, due.AddYears(1)), "5-second grace; practice never expires.");
        Check(PracticeTiming.FinishedAt(due, due.AddSeconds(3)) == due && PracticeTiming.FinishedAt(due, due.AddSeconds(-3)) == due.AddSeconds(-3) &&
            PracticeTiming.FinishedAt(null, due) == due, "Finished time capped at the deadline.");
        Check(PracticeTiming.SuggestedMinutes(10) == 15 && PracticeTiming.SuggestedMinutes(1) == 5 && PracticeTiming.SuggestedMinutes(7) == 15 &&
            PracticeTiming.SuggestedMinutes(50) == 75, "Suggested duration 1.5 min/question rounded up to 5.");
        Console.WriteLine("MOCK_CRITERIA_DEADLINE_GRACE_SUGGESTED=PASS (isolated; no SQL/API)");
    }

    // Loại 2: 3 đáp án, đáp án Id*10 đúng; loại 3: 3 đáp án, hai đáp án đúng.
    private static Question Make(int id, int type, string? explanation)
    {
        var question = new Question { Id = id, SubjectId = 1, Status = 1, QuestionTypeId = type, Content = $"Q{id}", Explanation = explanation };
        for (var i = 0; i < 3; i++)
            question.Answers.Add(new Answer { Id = id * 10 + i, QuestionId = id, Status = 1, Content = $"Option {i}",
                IsCorrect = i == 0 || (type == 3 && i == 1) });
        return question;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid practice input accepted.");
    }
}
