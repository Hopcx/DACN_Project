using System.Text.Json;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

internal static class AttemptSnapshotSmoke
{
    public static void Run()
    {
        var at = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        Check(AttemptTiming.CanStart(at, at, at.AddMinutes(1)), "Schedule start boundary must be inclusive.");
        Check(!AttemptTiming.CanStart(at.AddMinutes(1), at, at.AddMinutes(1)), "Schedule end boundary must be exclusive.");
        Check(AttemptTiming.Deadline(at, 30, at.AddMinutes(10)) == at.AddMinutes(10), "Schedule end must cap deadline.");
        Check(AttemptTiming.Deadline(at, 5, at.AddMinutes(10)) == at.AddMinutes(5), "Duration must cap deadline.");
        var deadline = at.AddMinutes(5);
        Check(!AttemptTiming.IsExpired(deadline.AddTicks(-1), deadline), "One tick before deadline must remain open.");
        Check(AttemptTiming.IsExpired(deadline, deadline), "Exact deadline must be expired.");
        Check(AttemptTiming.IsExpired(deadline.AddTicks(1), deadline), "One tick after deadline must be expired.");
        var exam = new Exam { Id = 1, SubjectId = 1, Name = "Snapshot QA", Status = 1, NumberOfQuestions = 2 };
        var variant = new ExamDetail { Id = 2, ExamId = 1, Code = "QA", Status = 1 };
        for (var i = 1; i <= 2; i++)
        {
            var question = new Question { Id = i, SubjectId = 1, Status = 1, QuestionTypeId = i + 1, Content = $"Question {i}",
                Explanation = i == 1 ? "  Vì A đúng  " : "   " };
            question.Answers.Add(new Answer { Id = i * 10, QuestionId = i, Status = 1, Content = "Option A", IsCorrect = true });
            question.Answers.Add(new Answer { Id = i * 10 + 1, QuestionId = i, Status = 1, Content = "Option B", IsCorrect = i == 2 });
            variant.ExamDetailQuestions.Add(new ExamDetailQuestion { Id = i, ExamDetailId = 2, QuestionId = i, Question = question, Point = 5 });
        }
        var snapshot = AttemptSnapshotFactory.Create(exam, variant);
        // To-be T4: snapshot v2 chụp lời giải (trim, rỗng → null)
        Check(snapshot.Version == 4 && AttemptSnapshot.CurrentVersion == 4, "New snapshots must be version 4.");
        Check(snapshot.Questions[0].Explanation == "Vì A đúng", "Explanation must be captured and trimmed.");
        Check(snapshot.Questions[1].Explanation == null, "Blank explanation must become null.");
        var serialized = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<AttemptSnapshot>(serialized)!;
        Check(restored.Questions[1].Options.All(x => x.IsCorrect), "Server snapshot lost answer keys on serialization.");
        Check(restored.Questions[0].Explanation == "Vì A đúng", "Explanation lost on serialization.");
        var sourceQuestion = variant.ExamDetailQuestions.First().Question;
        sourceQuestion.Content = "Changed after start";
        sourceQuestion.Explanation = "Changed explanation";
        sourceQuestion.Answers.First().Content = "Changed option";
        sourceQuestion.Answers.First().IsCorrect = false;
        sourceQuestion.Answers.Clear();
        Check(JsonSerializer.Serialize(snapshot) == serialized, "Snapshot changed with the live question bank.");
        Check(JsonSerializer.Serialize(restored) == serialized, "Snapshot round-trip changed IDs/content/keys.");
        // Snapshot v1 (trước T4) không có Explanation: đọc bằng options của API vẫn được, Explanation = null
        const string v1 = "{\"version\":1,\"examId\":1,\"examDetailId\":2,\"examName\":\"Old\",\"variantCode\":\"QA\",\"questions\":[{\"id\":\"6f0c2d7e-0a3b-4b8e-9a51-2b7f4f7d1c11\",\"sourceQuestionId\":1,\"questionTypeId\":2,\"content\":\"Q\",\"point\":1,\"options\":[{\"id\":\"0b9f1c4a-5d2e-4f63-8a7b-1c2d3e4f5a6b\",\"sourceAnswerId\":10,\"content\":\"A\",\"isCorrect\":true}]}]}";
        var legacy = JsonSerializer.Deserialize<AttemptSnapshot>(v1, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(legacy.Version == 1 && legacy.Questions.Count == 1 && legacy.Questions[0].Explanation == null &&
            legacy.Questions[0].Options[0].IsCorrect, "Snapshot v1 must still deserialize with Explanation = null.");

        var safe = JsonSerializer.SerializeToElement(AttemptSnapshotFactory.ForStudent(restored));
        foreach (var question in safe.EnumerateArray())
        {
            Check(question.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "Content", "Id", "Media", "Options", "QuestionTypeId" }), "Unsafe question DTO fields (T15: + Media).");
            foreach (var option in question.GetProperty("Options").EnumerateArray())
                Check(option.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "Content", "Id" }), "Unsafe option DTO fields.");
        }
        var one = restored.Questions[0]; var many = restored.Questions[1];
        var selected = new List<Guid> { one.Options[0].Id };
        var draft = AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(one.Id, selected) });
        selected.Clear();
        Check(draft[0].SelectedAnswerIds.Count == 1 && draft[1].SelectedAnswerIds.Count == 0, "Draft must be detached and fill omitted questions with empty selections.");
        var cleared = AttemptSnapshotFactory.ValidateDraft(restored, Array.Empty<AttemptAnswerDto>());
        Check(cleared.All(x => x.SelectedAnswerIds.Count == 0), "Empty replacement did not clear draft.");
        var multi = AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(many.Id, many.Options.Select(x => x.Id).Reverse().ToArray()) });
        Check(multi[1].SelectedAnswerIds.SequenceEqual(many.Options.Select(x => x.Id)), "Multi-choice draft order must follow the snapshot.");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(Guid.NewGuid(), Array.Empty<Guid>()) }));
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(one.Id, new[] { many.Options[0].Id }) }));
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(one.Id, new[] { one.Options[0].Id, one.Options[0].Id }) }));
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, new[] { new AttemptAnswerDto(one.Id, one.Options.Select(x => x.Id).ToArray()) }));
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, new[] { draft[0], draft[0] }));
        Reject(() => AttemptSnapshotFactory.ValidateDraft(restored, null));
        Reject(() => AttemptSnapshotFactory.Create(exam, variant)); // source now lacks answers
        Console.WriteLine("ATTEMPT_SNAPSHOT_ISOLATION_JSON_ROUNDTRIP_SAFE_DTO_DRAFT_VALIDATION=PASS (isolated; no SQL/API)");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid snapshot/draft accepted.");
    }
}
