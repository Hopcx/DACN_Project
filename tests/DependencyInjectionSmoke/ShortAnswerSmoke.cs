using System.Text.Json;
using Project.Api.Controllers;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

/// <summary>To-be T16: câu điền ngắn — so khớp, chấm điểm (cùng một hàm ở mọi nơi), nháp, snapshot v5, DTO học viên, validator, Excel.</summary>
internal static class ShortAnswerSmoke
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("SHORT_ANSWER: " + message); }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (BadRequestException) { return; }
        throw new Exception("SHORT_ANSWER: " + message);
    }

    private static AttemptQuestionSnapshot Q(Guid id, AttemptShortAnswerSnapshot? rule, params string[] accepted) =>
        new(id, 10, 4, "Thủ đô?", 2, accepted.Select((x, i) => new AttemptOptionSnapshot(Guid.NewGuid(), i + 1, x, true)).ToArray(),
            "Giải thích", 1, [], [], rule);

    public static void Run()
    {
        // Bộ so khớp
        var plain = new AttemptShortAnswerSnapshot(false, false, 0);
        Check(ShortAnswerMatcher.Matches(plain, ["Hà Nội"], "  hà   NỘI "), "case + whitespace");
        Check(ShortAnswerMatcher.Matches(plain, ["Hà Nội"], "Hà Nội"), "NFC");
        Check(!ShortAnswerMatcher.Matches(plain, ["Hà Nội"], "Ha Noi"), "diacritics matter by default");
        Check(ShortAnswerMatcher.Matches(new(true, false, 0), ["Hà Nội", "Đà Nẵng"], "da nang"), "ignore diacritics incl. đ");
        Check(!ShortAnswerMatcher.Matches(plain, ["Hà Nội"], "   "), "blank never matches");
        Check(!ShortAnswerMatcher.Matches(plain, ["0.5"], "0,5"), "no numeric mode → text compare");
        var numeric = new AttemptShortAnswerSnapshot(false, true, 0);
        Check(ShortAnswerMatcher.Matches(numeric, ["0.5"], "0,5") && ShortAnswerMatcher.Matches(numeric, ["0,50"], "0.5"),
            "numeric , or .");
        Check(!ShortAnswerMatcher.Matches(numeric, ["1000"], "1.000,0") && !ShortAnswerMatcher.Matches(numeric, ["0.5"], "1/2"),
            "no thousand separators / fractions");
        Check(ShortAnswerMatcher.Matches(new(false, true, 0.01m), ["3.14"], "3,149") && !ShortAnswerMatcher.Matches(new(false, true, 0.01m), ["3.14"], "3.151"),
            "tolerance inclusive");
        Check(ShortAnswerMatcher.Matches(numeric, ["pi", "3.14"], "PI"), "non-numeric accepted still text-compared");
        Check(!ShortAnswerMatcher.TryParseNumber("1e5", out _) && ShortAnswerMatcher.TryParseNumber("-2,5", out var n) && n == -2.5m, "number shape");

        // Chấm: AttemptScoring, PracticeCheck, QuestionOutcomeRules dùng cùng AnswerGrading
        var q1 = Guid.NewGuid(); var q2 = Guid.NewGuid(); var q3 = Guid.NewGuid();
        var choiceRight = Guid.NewGuid();
        var snapshot = new AttemptSnapshot(5, 1, 1, "E", "V", [
            Q(q1, new(true, false, 0), "Hà Nội", "Thăng Long"),
            Q(q2, new(false, true, 0.1m), "9.8"),
            new AttemptQuestionSnapshot(q3, 11, 2, "Chọn", 1, [new(choiceRight, 1, "A", true), new(Guid.NewGuid(), 2, "B", false)])]);
        var answers = new[] { new AttemptAnswerDto(q1, [], " thang long "), new AttemptAnswerDto(q2, [], "9,75"), new AttemptAnswerDto(q3, [choiceRight]) };
        var score = AttemptScoring.Calculate(snapshot, answers, 0);
        Check(score.RawTotal == 5m, $"score 2+2+1, got {score.RawTotal}");
        var wrong = AttemptScoring.Calculate(snapshot, [new AttemptAnswerDto(q1, [], "Huế"), new AttemptAnswerDto(q2, [], "9.69")], 0);
        Check(wrong.RawTotal == 0, "wrong text / outside tolerance score zero");
        var outcomes = QuestionOutcomeRules.Evaluate(snapshot, answers);
        Check(outcomes[0].IsCorrect && outcomes[0].IsAnswered && outcomes[0].AnswerText == "thang long" && outcomes[0].SelectedSourceAnswerIds.Count == 0,
            "outcome stores normalized text");
        Check(outcomes[2].AnswerText == null, "choice outcome has no text");
        var blank = QuestionOutcomeRules.Evaluate(snapshot, []);
        Check(!blank[0].IsAnswered && blank[0].AnswerText == null, "blank short answer");

        // Nháp: loại 4 chỉ nhận text ≤ 200; loại 1–3 không nhận text
        var draft = AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(q1, [], "  Hà Nội  ")]);
        Check(draft[0].Text == "Hà Nội" && draft[1].Text == null && draft[2].Text == null, "canonical draft trims text");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(q1, [], new string('x', 201))]), "text > 200");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(q1, [snapshot.Questions[0].Options[0].Id], null)]),
            "short answer cannot select options");
        Reject(() => AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(q3, [choiceRight], "x")]), "choice with text");
        var draftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(draftJson.Contains("\"text\":\"Hà Nội\"") || draftJson.Contains("\"text\":\"H\\u00E0 N\\u1ED9i\""), "text serialized");
        Check(!JsonSerializer.Serialize(draft[2], new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains("text"), "null text omitted");

        // Kiểm tra từng câu (luyện tập)
        var (checkedDraft, checkedIds) = PracticeCheck.Apply(snapshot, draft, [], q2, null, "9,9");
        var result = PracticeCheck.Result(snapshot.Questions[1], checkedDraft.First(x => x.QuestionId == q2));
        Check(result.IsCorrect && result.Text == "9,9" && result.AcceptedAnswers!.SequenceEqual(["9.8"]) && result.CorrectAnswerIds.Count == 0,
            "practice check short answer");
        Reject(() => PracticeCheck.Apply(snapshot, draft, [], q1, null, "  "), "blank check rejected");
        try { PracticeCheck.Merge(snapshot, checkedDraft, checkedIds, [new AttemptAnswerDto(q2, [], "1")]); throw new Exception("SHORT_ANSWER: locked text changed"); }
        catch (PracticeQuestionLockedException) { }
        var kept = PracticeCheck.Merge(snapshot, checkedDraft, checkedIds, [new AttemptAnswerDto(q2, [], " 9,9 ")]);
        Check(kept.First(x => x.QuestionId == q2).Text == "9,9", "same locked text accepted");

        // DTO học viên: câu điền ngắn không có options (đáp án chấp nhận không lộ)
        var student = AttemptSnapshotFactory.ForStudent(snapshot);
        Check(student[0].Options.Count == 0 && student[1].Options.Count == 0 && student[2].Options.Count == 2, "no options for short answer");
        var studentJson = JsonSerializer.Serialize(student);
        Check(!studentJson.Contains("Thăng Long") && !studentJson.Contains("Thang Long") && !studentJson.Contains("9.8"), "accepted answers not leaked");

        // Snapshot v5: chụp quy tắc; câu loại 4 hợp lệ; JSON v4 cũ vẫn đọc được
        var question = new Question { Id = 7, Status = 1, QuestionTypeId = 4, Content = "Q", ShortAnswerIgnoreDiacritics = true, ShortAnswerNumeric = true,
            ShortAnswerTolerance = 0.5m, Answers = [new Answer { Id = 1, QuestionId = 7, Content = "1,5", IsCorrect = true, Status = 1 }] };
        Check(AttemptSnapshotFactory.QuestionShapeValid(question) && AttemptSnapshotFactory.AnswerSetValid(question), "type 4 valid for snapshot");
        var built = AttemptSnapshotFactory.BuildQuestion(question, 1);
        Check(built.ShortAnswer == new AttemptShortAnswerSnapshot(true, true, 0.5m), "v5 captures rule");
        question.Answers.First().IsCorrect = false;
        Check(!AttemptSnapshotFactory.AnswerSetValid(question), "type 4 requires all accepted");
        var v4 = JsonSerializer.Deserialize<AttemptSnapshot>(
            """{"version":4,"examId":1,"examDetailId":1,"examName":"E","variantCode":"V","questions":[{"id":"00000000-0000-0000-0000-000000000001","sourceQuestionId":5,"questionTypeId":2,"content":"Q","point":1,"options":[]}]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(v4.Questions[0].ShortAnswer == null, "v4 reads without ShortAnswer");

        // Validator câu hỏi
        QuestionCreateDto Dto(params string[] accepted) => new() { SubjectId = 1, QuestionTypeId = 4, Content = "Q",
            Answers = accepted.Select(x => new QuestionAnswerInputDto { Content = x, IsCorrect = true }).ToList() };
        QuestionService.Validate(Dto("Hà Nội"));
        Reject(() => QuestionService.Validate(Dto()), "no accepted answer");
        Reject(() => QuestionService.Validate(Dto(Enumerable.Range(0, 11).Select(i => $"a{i}").ToArray())), "> 10 accepted");
        Reject(() => QuestionService.Validate(Dto(new string('x', 201))), "> 200 chars");
        Reject(() => QuestionService.Validate(Dto("Hà Nội", "hà  nội")), "duplicate after normalize");
        var diacritics = Dto("Hà Nội", "Ha Noi"); QuestionService.Validate(diacritics);
        diacritics.ShortAnswerIgnoreDiacritics = true; Reject(() => QuestionService.Validate(diacritics), "duplicate when ignoring diacritics");
        var notAccepted = Dto("A"); notAccepted.Answers[0].IsCorrect = false; Reject(() => QuestionService.Validate(notAccepted), "IsCorrect false");
        var tol = Dto("1"); tol.ShortAnswerTolerance = 0.1m; Reject(() => QuestionService.Validate(tol), "tolerance without numeric");
        tol.ShortAnswerNumeric = true; QuestionService.Validate(tol);
        tol.ShortAnswerTolerance = -1; Reject(() => QuestionService.Validate(tol), "negative tolerance");
        tol.ShortAnswerTolerance = 0.0000001m; Reject(() => QuestionService.Validate(tol), "> 6 decimals");

        // Review T16: khóa thống kê — số dạng chuẩn khi bật so số; cắt 200 ký tự không tách cặp surrogate
        Check(ShortAnswerMatcher.StatsKey(numeric, "3,50") == "3.5" && ShortAnswerMatcher.StatsKey(numeric, " 3.5 ") == "3.5" &&
            ShortAnswerMatcher.StatsKey(numeric, "ba") == "ba" && ShortAnswerMatcher.StatsKey(plain, "3,50") == "3,50" &&
            ShortAnswerMatcher.StatsKey(plain, "  ") == null, "stats key");
        var emoji = new string('x', 199) + "\U0001F600";
        var cut = ShortAnswerMatcher.StatsKey(plain, emoji)!;
        Check(cut.Length == 199 && !char.IsHighSurrogate(cut[^1]), "stats key keeps surrogate pairs whole");
        var numericOutcomes = QuestionOutcomeRules.Evaluate(snapshot, [new AttemptAnswerDto(q2, [], "9,50")]);
        Check(numericOutcomes[1].AnswerText == "9.5", "numeric outcome text canonical");

        // Review T16: bài làm chỉ có khoảng trắng = chưa trả lời (nháp lưu null)
        var spaces = AttemptSnapshotFactory.ValidateDraft(snapshot, [new AttemptAnswerDto(q1, [], "   ")]);
        Check(spaces[0].Text == null && !AnswerGrading.IsAnswered(snapshot.Questions[0], spaces[0]), "whitespace text is unanswered");
        var mergedSpaces = PracticeCheck.Merge(snapshot, checkedDraft, checkedIds, [new AttemptAnswerDto(q1, [], "  ")]);
        Check(mergedSpaces.First(x => x.QuestionId == q1).Text == null && mergedSpaces.First(x => x.QuestionId == q2).Text == "9,9",
            "merge drops whitespace text, keeps locked");

        // Review T16: báo cáo lớp — câu hay sai loại 4 hiện câu trả lời sai phổ biến nhất (hòa: thứ tự chữ), không có đáp án nhiễu
        var users = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var reportOutcomes = users.Select((u, i) => new ReportOutcome(u, 77, 1, i == 0, [],
            i == 0 ? "ha noi" : i <= 3 ? "hue" : i <= 6 ? "da nang" : i <= 8 ? "sai gon" : null)).ToList();
        var report = ClassReport.Build(reportOutcomes, new AnalysisThresholds(), new HashSet<int>());
        var hard = report.HardQuestions.Single();
        Check(hard.WrongText == "da nang" && hard.DistractorCount == 3 && hard.DistractorAnswerId == null && hard.Correct == 1,
            $"report wrong text tie-break, got {hard.WrongText}/{hard.DistractorCount}");
        var choiceReport = ClassReport.Build(users.Select((u, i) => new ReportOutcome(u, 78, 1, false, [5])).ToList(),
            new AnalysisThresholds(), new HashSet<int> { 9 });
        Check(choiceReport.HardQuestions.Single() is { DistractorAnswerId: 5, WrongText: null, DistractorCount: 10 }, "choice keeps distractor");

        // Excel: cột G
        Check(ShortAnswerOptions.Parse("").Error == null && ShortAnswerOptions.Parse("nodiacritics; numeric=0.01") is { IgnoreDiacritics: true, Numeric: true, Tolerance: 0.01m },
            "parse options");
        Check(ShortAnswerOptions.Parse("numeric;numeric").Error != null && ShortAnswerOptions.Parse("numeric=-1").Error != null &&
            ShortAnswerOptions.Parse("abc").Error != null, "reject bad options");
        Check(ShortAnswerOptions.Format(true, true, 0.01m) == "nodiacritics;numeric=0.01" && ShortAnswerOptions.Format(false, true, 0m) == "numeric" &&
            ShortAnswerOptions.Format(null, null, null) == "", "format options");
        Console.WriteLine("SHORT_ANSWER_MATCH_GRADE_DRAFT_CHECK_DTO_SNAPSHOT_V5_VALIDATE_EXCEL=PASS (isolated; no SQL/API)");
    }
}
