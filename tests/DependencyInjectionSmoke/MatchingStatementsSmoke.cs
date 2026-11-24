using System.Text.Json;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

/// <summary>To-be T17: ghép cặp (loại 5) và Đúng/Sai nhiều mệnh đề (loại 6) — snapshot v6, DTO, nháp, chấm, kiểm tra từng câu, validator.</summary>
internal static class MatchingStatementsSmoke
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("MATCHING_STATEMENTS: " + message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (BadRequestException) { return; }
        throw new Exception("MATCHING_STATEMENTS: " + message);
    }

    public static void Run()
    {
        // Snapshot v6: vế phải có Guid riêng, thứ tự xáo một lần
        var pairs = new[] { ("Hà Nội", "Việt Nam"), ("Tokyo", "Nhật Bản"), ("Paris", "Pháp"), ("Lima", "Peru"), ("Oslo", "Na Uy"), ("Rome", "Ý") };
        var matchingQ = new Question { Id = 50, Status = 1, QuestionTypeId = 5, Content = "Ghép thủ đô",
            Answers = pairs.Select((p, i) => new Answer { Id = i + 1, QuestionId = 50, Content = p.Item1, MatchContent = " " + p.Item2 + " ", IsCorrect = false, Status = 1 }).ToList() };
        Check(AttemptSnapshotFactory.QuestionShapeValid(matchingQ) && AttemptSnapshotFactory.AnswerSetValid(matchingQ), "matching valid");
        var m = AttemptSnapshotFactory.BuildQuestion(matchingQ, 2, new Random(7));
        var matchIds = m.Options.Select(x => x.MatchId!.Value).ToList();
        Check(m.Options.All(x => x.IsCorrect && x.MatchContent == x.MatchContent!.Trim()) && matchIds.Distinct().Count() == 6 &&
            !matchIds.Intersect(m.Options.Select(x => x.Id)).Any(), "own right-side Guids, trimmed");
        Check(m.MatchOrder!.OrderBy(x => x).SequenceEqual(matchIds.OrderBy(x => x)) && !m.MatchOrder.SequenceEqual(matchIds), "right sides shuffled");
        // Review T17: không truyền seed → mỗi lần chụp xáo độc lập (so vị trí theo vế trái; trùng cả 3 lần xác suất ≈ 1/720²)
        int[] Positions(AttemptQuestionSnapshot q) => q.Options.Select(o => q.MatchOrder!.ToList().IndexOf(o.MatchId!.Value)).ToArray();
        var shuffles = Enumerable.Range(0, 3).Select(_ => Positions(AttemptSnapshotFactory.BuildQuestion(matchingQ, 1))).ToList();
        Check(shuffles.Skip(1).Any(x => !x.SequenceEqual(shuffles[0])), "independent shuffles per capture");
        matchingQ.Answers.First().Content = "  Hà Nội  ";
        Check(AttemptSnapshotFactory.BuildQuestion(matchingQ, 1).Options[0].Content == "Hà Nội", "left side trimmed in snapshot");
        matchingQ.Answers.First().MatchContent = new string('x', 2001);
        Check(!AttemptSnapshotFactory.AnswerSetValid(matchingQ), "right side > 2000 invalid");
        matchingQ.Answers.First().MatchContent = null;
        Check(!AttemptSnapshotFactory.AnswerSetValid(matchingQ), "pair without right side invalid");

        var statementsQ = new Question { Id = 60, Status = 1, QuestionTypeId = 6, Content = "Mệnh đề",
            Answers = [new Answer { Id = 11, QuestionId = 60, Content = "Trái Đất quay quanh Mặt Trời", IsCorrect = true, Status = 1 },
                new Answer { Id = 12, QuestionId = 60, Content = "Nước sôi ở 50°C", IsCorrect = false, Status = 1 },
                new Answer { Id = 13, QuestionId = 60, Content = "2 + 2 = 4", IsCorrect = true, Status = 1 }] };
        Check(AttemptSnapshotFactory.AnswerSetValid(statementsQ), "statements valid");
        var s = AttemptSnapshotFactory.BuildQuestion(statementsQ, 1);
        Check(s.MatchOrder == null && s.Options.All(x => x.MatchId == null), "statements have no matches");
        var choiceId = Guid.NewGuid(); var choiceOk = Guid.NewGuid();
        var snapshot = new AttemptSnapshot(6, 1, 1, "E", "V", [m, s,
            new AttemptQuestionSnapshot(choiceId, 70, 2, "Chọn", 1, [new(choiceOk, 1, "A", true), new(Guid.NewGuid(), 2, "B", false)])]);

        // DTO học viên: vế trái + vế phải theo thứ tự đã xáo, không có cặp/cờ đúng
        var student = AttemptSnapshotFactory.ForStudent(snapshot);
        Check(student[0].Options.Select(x => x.Id).SequenceEqual(m.Options.Select(x => x.Id)) &&
            student[0].Matches!.Select(x => x.Id).SequenceEqual(m.MatchOrder) && student[1].Matches == null && student[2].Matches == null,
            "student DTO options/matches");
        var json = JsonSerializer.Serialize(student, Web);
        Check(!json.Contains("isCorrect", StringComparison.OrdinalIgnoreCase) && !json.Contains("matchId", StringComparison.OrdinalIgnoreCase) &&
            !json.Contains("sourceAnswerId", StringComparison.OrdinalIgnoreCase), "no pairing/correctness in student JSON");
        Check(!JsonSerializer.Serialize(student[2], Web).Contains("matches"), "choice DTO omits matches");

        // Nháp
        AttemptItemDto Pair(int left, int right) => new(m.Options[left].Id, m.Options[right].MatchId);
        var allRight = Enumerable.Range(0, 6).Select(i => Pair(i, i)).ToArray();
        var draft = AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], null, allRight.Reverse().ToArray())]);
        Check(draft[0].Items!.SequenceEqual(allRight) && draft[1].Items == null && draft[2].Items == null, "canonical items order, empty → null");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], null, [Pair(0, 1), Pair(1, 1)])]), "right side used twice");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], null, [new AttemptItemDto(m.Options[0].Id, Guid.NewGuid())])]), "unknown right side");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], null, [new AttemptItemDto(m.Options[0].Id, m.Options[0].MatchId, true)])]), "matching with value");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], null, [Pair(0, 0), Pair(0, 1)])]), "left side twice");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [m.Options[0].Id])]), "matching with selected ids");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(s.Id, [], null, [new AttemptItemDto(s.Options[0].Id)])]), "statement without value");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(s.Id, [], null, [new AttemptItemDto(m.Options[0].Id, null, true)])]), "foreign option");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(choiceId, [choiceOk], null, [new AttemptItemDto(choiceOk, null, true)])]), "items on choice");
        // Review T17: thêm các ca từ chối / chấp nhận
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(m.Id, [], "x", [Pair(0, 0)])]), "items with text");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(s.Id, [], null, [new AttemptItemDto(s.Options[0].Id, m.Options[0].MatchId, true)])]),
            "statement item with matchId");
        var shortQ = new AttemptQuestionSnapshot(Guid.NewGuid(), 80, 4, "Điền", 1, [new(Guid.NewGuid(), 1, "x", true)]);
        var withShort = new AttemptSnapshot(6, 1, 1, "E", "V", [shortQ]);
        Reject(() => AttemptSnapshotFactory.ValidateDraft(withShort, [new AttemptAnswerDto(shortQ.Id, [], "x", [new AttemptItemDto(shortQ.Options[0].Id, null, true)])]),
            "items on short answer");
        var spaces = AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(s.Id, [], "  ", [St0()])]);
        Check(spaces[1].Text == null && spaces[1].Items!.Count == 1, "whitespace text ignored on statements");
        AttemptItemDto St0() => new(s.Options[0].Id, null, true);
        var draftJson = JsonSerializer.Serialize(draft, Web);
        var back = JsonSerializer.Deserialize<List<AttemptAnswerDto>>(draftJson, Web)!;
        Check(back[0].Items!.SequenceEqual(allRight) && !JsonSerializer.Serialize(draft[2], Web).Contains("items"), "items JSON round-trip, null omitted");

        // Chấm: tất cả-hoặc-không; thiếu mục = sai nhưng vẫn "đã trả lời"
        AttemptItemDto St(int i, bool v) => new(s.Options[i].Id, null, v);
        var answers = new[] { new AttemptAnswerDto(m.Id, [], null, allRight), new AttemptAnswerDto(s.Id, [], null, [St(0, true), St(1, false), St(2, true)]),
            new AttemptAnswerDto(choiceId, [choiceOk]) };
        Check(AttemptScoring.Calculate(snapshot, answers, 0).RawTotal == 4m, "all correct = 2 + 1 + 1");
        var swapped = Enumerable.Range(0, 6).Select(i => Pair(i, i < 2 ? 1 - i : i)).ToArray();
        var wrong = new[] { new AttemptAnswerDto(m.Id, [], null, swapped), new AttemptAnswerDto(s.Id, [], null, [St(0, true), St(1, false)]) };
        Check(AttemptScoring.Calculate(snapshot, wrong, 0).RawTotal == 0m, "one wrong pair / missing statement = 0");
        var outcomes = QuestionOutcomeRules.Evaluate(snapshot, wrong);
        Check(outcomes[0] is { IsAnswered: true, IsCorrect: false } && outcomes[1] is { IsAnswered: true, IsCorrect: false } &&
            outcomes[0].SelectedSourceAnswerIds.Count == 0 && outcomes[0].AnswerText == null, "outcomes answered but wrong");
        Check(QuestionOutcomeRules.Evaluate(snapshot, answers).Take(2).All(x => x.IsCorrect), "outcomes agree with score");
        var allFalse = new AttemptQuestionSnapshot(Guid.NewGuid(), 61, 6, "Sai hết", 1,
            [new(Guid.NewGuid(), 1, "A", false), new(Guid.NewGuid(), 2, "B", false)]);
        Check(AnswerGrading.IsCorrect(allFalse, new AttemptAnswerDto(allFalse.Id, [], null,
            allFalse.Options.Select(x => new AttemptItemDto(x.Id, null, false)).ToArray())), "all-false statements can be answered correctly");
        Check(AnswerGrading.CorrectItems(m)!.Select(x => x.MatchId!.Value).SequenceEqual(matchIds) &&
            AnswerGrading.CorrectItems(s)!.Select(x => x.Value).SequenceEqual(new bool?[] { true, false, true }) &&
            AnswerGrading.CorrectItems(snapshot.Questions[2]) == null, "correct items");

        // Kiểm tra từng câu: kết quả có câu trả lời + đáp án đúng từng mục; câu đã chốt khóa Items
        var (checkedDraft, checkedIds) = PracticeCheck.Apply(snapshot, [], [], s.Id, null, null, [St(0, true), St(1, true)]);
        var result = PracticeCheck.Result(s, checkedDraft.First(x => x.QuestionId == s.Id));
        Check(!result.IsCorrect && result.Items!.Count == 2 && result.CorrectItems!.Count == 3 && result.CorrectAnswerIds.Count == 0, "check statements");
        Reject(() => PracticeCheck.Apply(snapshot, [], [], m.Id, null, null, []), "empty items check");
        try { PracticeCheck.Merge(snapshot, checkedDraft, checkedIds, [new AttemptAnswerDto(s.Id, [], null, [St(0, false)])]); throw new Exception("MATCHING_STATEMENTS: locked items changed"); }
        catch (PracticeQuestionLockedException) { }
        var kept = PracticeCheck.Merge(snapshot, checkedDraft, checkedIds, [new AttemptAnswerDto(s.Id, [], null, [St(1, true), St(0, true)]),
            new AttemptAnswerDto(m.Id, [], null, [Pair(0, 3)])]);
        Check(kept.First(x => x.QuestionId == s.Id).Items!.Count == 2 && kept.First(x => x.QuestionId == m.Id).Items!.Single() == Pair(0, 3),
            "same locked items (other order) accepted; unlocked question saved");

        // Snapshot v5 cũ vẫn đọc được
        var v5 = JsonSerializer.Deserialize<AttemptSnapshot>(
            """{"version":5,"examId":1,"examDetailId":1,"examName":"E","variantCode":"V","questions":[{"id":"00000000-0000-0000-0000-000000000001","sourceQuestionId":5,"questionTypeId":2,"content":"Q","point":1,"options":[{"id":"00000000-0000-0000-0000-000000000002","sourceAnswerId":1,"content":"A","isCorrect":true}]}]}""", Web)!;
        Check(v5.Questions[0].MatchOrder == null && v5.Questions[0].Options[0].MatchId == null, "v5 reads");

        // Validator câu hỏi
        QuestionCreateDto Dto(int type, params (string Left, string? Right, bool Correct)[] items) => new() { SubjectId = 1, QuestionTypeId = type, Content = "Q",
            Answers = items.Select(x => new QuestionAnswerInputDto { Content = x.Left, MatchContent = x.Right, IsCorrect = x.Correct }).ToList() };
        QuestionService.Validate(Dto(5, ("A", "1", false), ("B", "2", false)));
        Reject(() => QuestionService.Validate(Dto(5, ("A", "1", true))), "1 pair");
        Reject(() => QuestionService.Validate(Dto(5, Enumerable.Range(0, 11).Select(i => ($"L{i}", (string?)$"R{i}", true)).ToArray())), "11 pairs");
        Reject(() => QuestionService.Validate(Dto(5, ("A", "1", true), ("a", "2", true))), "duplicate left");
        Reject(() => QuestionService.Validate(Dto(5, ("A", "1", true), ("B", " 1 ", true))), "duplicate right");
        Reject(() => QuestionService.Validate(Dto(5, ("A", "1", true), ("B", null, true))), "missing right");
        QuestionService.Validate(Dto(6, ("P", null, false), ("Q", null, false)));
        Reject(() => QuestionService.Validate(Dto(6, ("P", null, true), ("p", null, false))), "duplicate statements");
        Reject(() => QuestionService.Validate(Dto(6, ("P", null, true))), "1 statement");
        Console.WriteLine("MATCHING_STATEMENTS_SNAPSHOT_V6_DTO_DRAFT_GRADE_CHECK_VALIDATE=PASS (isolated; no SQL/API)");
    }
}
