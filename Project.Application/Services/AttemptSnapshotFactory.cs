using Project.Application.DTOs.AttemptDTO;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

// Pure preparation for the attempt store: no DB writes, eligibility, timing or grading.
public static class AttemptSnapshotFactory
{
    public static AttemptSnapshot Create(Exam exam, ExamDetail variant)
    {
        if (exam.Id <= 0 || variant.Id <= 0 || variant.ExamId != exam.Id || exam.Status != 1 || variant.Status != 1)
            throw new ArgumentException("Bài thi và mã đề phải được công khai và thuộc cùng bài thi.");
        var links = variant.ExamDetailQuestions.OrderBy(x => x.Id).ToArray();
        if (exam.NumberOfQuestions <= 0 || links.Length != exam.NumberOfQuestions ||
            links.Select(x => x.QuestionId).Distinct().Count() != links.Length)
            throw new ArgumentException("Mã đề phải có đúng số câu, không trùng câu hỏi.");
        var questions = new List<AttemptQuestionSnapshot>();
        foreach (var link in links)
        {
            var question = link.Question;
            if (link.ExamDetailId != variant.Id || question == null || question.Id != link.QuestionId ||
                question.SubjectId != exam.SubjectId || !QuestionShapeValid(question) ||
                !double.IsFinite(link.Point) || link.Point < 0)
                throw new ArgumentException("Câu hỏi của mã đề không hợp lệ hoặc không được hỗ trợ.");
            if (!AnswerSetValid(question))
                throw new ArgumentException("Tập đáp án của câu hỏi không hợp lệ.");
            questions.Add(BuildQuestion(question, link.Point));
        }
        return new AttemptSnapshot(AttemptSnapshot.CurrentVersion, exam.Id, variant.Id, exam.Name, variant.Code, questions.AsReadOnly());
    }

    // Dùng chung cho Exam và Practice (To-be T5): câu hoạt động, loại 1–6 (T16: 4 điền ngắn; T17: 5 ghép cặp, 6 nhiều mệnh đề), có nội dung.
    public static bool QuestionShapeValid(Question question) =>
        question.Id > 0 && question.Status == 1 && QuestionKinds.IsSupported(question.QuestionTypeId) &&
        !string.IsNullOrWhiteSpace(question.Content);

    // Đáp án hoạt động: ≥ 2, không trùng, có nội dung; Đúng/Sai đúng 2; loại 1–2 đúng một đáp án đúng; loại 3 ≥ 1.
    // T16: loại 4 = 1–10 đáp án chấp nhận, tất cả IsCorrect, mỗi đáp án ≤ 200 ký tự; sai số ≥ 0.
    // T17: loại 5 = 2–10 cặp, mỗi cặp có vế phải, vế trái/vế phải không trùng; loại 6 = 2–10 mệnh đề (Đúng/Sai tùy ý).
    public static bool AnswerSetValid(Question question)
    {
        var answers = question.Answers.Where(x => x.Status == 1).ToArray();
        var correct = answers.Count(x => x.IsCorrect);
        var common = answers.Select(x => x.Id).Distinct().Count() == answers.Length &&
            !answers.Any(x => x.Id <= 0 || x.QuestionId != question.Id || string.IsNullOrWhiteSpace(x.Content));
        if (question.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId)
            return common && answers.Length is >= 1 and <= ShortAnswerMatcher.MaxAccepted && correct == answers.Length &&
                answers.All(x => x.Content.Trim().Length <= ShortAnswerMatcher.MaxTextLength) &&
                (question.ShortAnswerTolerance ?? 0) is >= 0 and <= ShortAnswerMatcher.MaxTolerance;
        if (question.QuestionTypeId == QuestionKinds.Matching)
            return common && answers.Length is >= QuestionKinds.MinItems and <= QuestionKinds.MaxItems &&
                answers.All(x => !string.IsNullOrWhiteSpace(x.MatchContent) && x.MatchContent.Trim().Length <= 2000 && x.Content.Trim().Length <= 2000) &&
                answers.Select(x => x.Content.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == answers.Length &&
                answers.Select(x => x.MatchContent!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == answers.Length;
        if (question.QuestionTypeId == QuestionKinds.Statements)
            return common && answers.Length is >= QuestionKinds.MinItems and <= QuestionKinds.MaxItems &&
                answers.All(x => x.Content.Trim().Length <= 2000);
        return answers.Length >= 2 && common &&
            !(question.QuestionTypeId == 1 && answers.Length != 2) &&
            !(question.QuestionTypeId is 1 or 2 && correct != 1) && correct != 0;
    }

    // Chụp một câu: đáp án theo Id, Guid mới, lời giải trim (rỗng → null). Gọi sau khi đã kiểm hai hàm trên.
    // T17: ghép cặp — vế phải có Guid riêng (MatchId) và thứ tự vế phải được xáo một lần (MatchOrder); random cho test.
    public static AttemptQuestionSnapshot BuildQuestion(Question question, double point, Random? random = null)
    {
        var matching = question.QuestionTypeId == QuestionKinds.Matching;
        var itemized = QuestionKinds.HasItems(question.QuestionTypeId);   // review T17: vế trái/mệnh đề cắt khoảng trắng như vế phải
        var options = Array.AsReadOnly(question.Answers.Where(x => x.Status == 1).OrderBy(x => x.Id).Select(answer =>
            new AttemptOptionSnapshot(Guid.NewGuid(), answer.Id, itemized ? answer.Content.Trim() : answer.Content, matching || answer.IsCorrect,
                matching ? Guid.NewGuid() : null, matching ? answer.MatchContent!.Trim() : null)).ToArray());
        IReadOnlyList<Guid>? matchOrder = null;
        if (matching)
        {
            var order = options.Select(x => x.MatchId!.Value).ToArray();
            (random ?? Random.Shared).Shuffle(order);
            matchOrder = Array.AsReadOnly(order);
        }
        return new(Guid.NewGuid(), question.Id, question.QuestionTypeId, question.Content, point, options,
            string.IsNullOrWhiteSpace(question.Explanation) ? null : question.Explanation.Trim(),
            question.ChapterId, Array.AsReadOnly(question.QuestionTags.Select(x => x.TagId).Distinct().OrderBy(x => x).ToArray()),
            // v4 (T15): media đang gắn và còn hoạt động, theo thứ tự; người gọi phải Include(QuestionMedia).ThenInclude(Media)
            Array.AsReadOnly(question.QuestionMedia.Where(x => x.Media != null && x.Media.Status == 1).OrderBy(x => x.SortOrder)
                .Select(x => new AttemptMediaSnapshot(x.MediaId, x.Media.Kind, x.Media.Width, x.Media.Height)).ToArray()),
            // v5 (T16): quy tắc so khớp của câu điền ngắn
            question.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId
                ? new AttemptShortAnswerSnapshot(question.ShortAnswerIgnoreDiacritics == true, question.ShortAnswerNumeric == true,
                    question.ShortAnswerNumeric == true ? question.ShortAnswerTolerance ?? 0 : 0)
                : null,
            matchOrder);
    }

    // To-be T15: mediaUrl ký URL cho đúng học viên đang xem; null → không cấp URL (media rỗng).
    // To-be T16: câu điền ngắn không có lựa chọn — Options là đáp án chấp nhận nên KHÔNG BAO GIỜ trả cho học viên ở đây.
    // To-be T17: ghép cặp — options = vế trái, matches = vế phải theo MatchOrder (Id riêng); không có cờ đúng/sai, không có cặp.
    public static IReadOnlyList<AttemptQuestionDto> ForStudent(AttemptSnapshot snapshot, Func<Guid, string>? mediaUrl = null) =>
        Array.AsReadOnly(snapshot.Questions.Select(question => new AttemptQuestionDto(
            question.Id, question.QuestionTypeId, question.Content,
            AnswerGrading.IsShortAnswer(question) ? Array.Empty<AttemptOptionDto>()
                : Array.AsReadOnly(question.Options.Select(option => new AttemptOptionDto(option.Id, option.Content)).ToArray()),
            MediaFor(question, mediaUrl), MatchesFor(question))).ToArray());

    /// <summary>T17: vế phải của câu ghép cặp theo thứ tự đã xáo (null với loại khác).</summary>
    public static IReadOnlyList<AttemptOptionDto>? MatchesFor(AttemptQuestionSnapshot question)
    {
        if (question.QuestionTypeId != QuestionKinds.Matching || question.MatchOrder == null) return null;
        var byId = question.Options.Where(x => x.MatchId != null).ToDictionary(x => x.MatchId!.Value, x => x.MatchContent ?? "");
        return Array.AsReadOnly(question.MatchOrder.Where(byId.ContainsKey).Select(id => new AttemptOptionDto(id, byId[id])).ToArray());
    }

    public static IReadOnlyList<AttemptMediaDto> MediaFor(AttemptQuestionSnapshot question, Func<Guid, string>? mediaUrl) =>
        mediaUrl == null || question.Media == null ? Array.Empty<AttemptMediaDto>()
            : Array.AsReadOnly(question.Media.Select(m => new AttemptMediaDto(m.Id, m.Kind, m.Width, m.Height, mediaUrl(m.Id))).ToArray());

    // Full replacement: omitted questions become unanswered. Validate all input before
    // returning a canonical, detached draft; the future store owns revision and transaction.
    // To-be T16: loại 4 chỉ nhận Text (≤ 200 ký tự sau trim, không chọn đáp án); loại 1–3 không nhận Text.
    // To-be T17: loại 5–6 chỉ nhận Items (mỗi mục một vế trái/mệnh đề, không trùng; loại 5 MatchId thuộc câu, không dùng lại;
    // loại 6 Value); loại 1–4 không nhận Items. Nháp chuẩn: Items theo thứ tự lựa chọn, rỗng → null.
    public static IReadOnlyList<AttemptAnswerDto> ValidateDraft(AttemptSnapshot snapshot,
        IReadOnlyList<AttemptAnswerDto>? answers)
    {
        const string invalid = "Câu hỏi hoặc đáp án không thuộc lượt thi, bị trùng hoặc chọn quá số lượng.";
        if (answers == null || answers.Count > snapshot.Questions.Count ||
            answers.Any(x => x == null || x.SelectedAnswerIds == null) ||
            answers.Select(x => x.QuestionId).Distinct().Count() != answers.Count)
            throw new ArgumentException("Danh sách câu trả lời thiếu hoặc chứa câu hỏi trùng.");
        var questions = snapshot.Questions.ToDictionary(x => x.Id);
        foreach (var answer in answers)
        {
            if (!questions.TryGetValue(answer.QuestionId, out var question)) throw new ArgumentException(invalid);
            if (AnswerGrading.HasItems(question))
            {
                ValidateItems(question, answer);
                continue;
            }
            if (answer.Items is { Count: > 0 }) throw new ArgumentException(invalid);
            if (AnswerGrading.IsShortAnswer(question))
            {
                if (answer.SelectedAnswerIds.Count > 0 || (answer.Text?.Trim().Length ?? 0) > ShortAnswerMatcher.MaxTextLength)
                    throw new ArgumentException($"Câu điền ngắn chỉ nhận câu trả lời tối đa {ShortAnswerMatcher.MaxTextLength} ký tự.");
                continue;
            }
            if (!string.IsNullOrEmpty(answer.Text) ||
                answer.SelectedAnswerIds.Distinct().Count() != answer.SelectedAnswerIds.Count ||
                answer.SelectedAnswerIds.Any(id => !question.Options.Any(option => option.Id == id)) ||
                (question.QuestionTypeId is 1 or 2 && answer.SelectedAnswerIds.Count > 1))
                throw new ArgumentException(invalid);
        }
        var incoming = answers.ToDictionary(x => x.QuestionId);
        return Array.AsReadOnly(snapshot.Questions.Select(question =>
        {
            incoming.TryGetValue(question.Id, out var answer);
            if (AnswerGrading.IsShortAnswer(question))
                return new AttemptAnswerDto(question.Id, Array.Empty<Guid>(),
                    string.IsNullOrWhiteSpace(answer?.Text) ? null : answer.Text.Trim());
            if (AnswerGrading.HasItems(question))
            {
                var items = (answer?.Items ?? []).ToDictionary(x => x.OptionId);
                var ordered = question.Options.Where(option => items.ContainsKey(option.Id)).Select(option =>
                    question.QuestionTypeId == QuestionKinds.Matching
                        ? new AttemptItemDto(option.Id, items[option.Id].MatchId)
                        : new AttemptItemDto(option.Id, null, items[option.Id].Value)).ToArray();
                return new AttemptAnswerDto(question.Id, Array.Empty<Guid>(), null, ordered.Length == 0 ? null : Array.AsReadOnly(ordered));
            }
            return new AttemptAnswerDto(question.Id, Array.AsReadOnly(question.Options.Where(option => answer != null &&
                answer.SelectedAnswerIds.Contains(option.Id)).Select(option => option.Id).ToArray()));
        }).ToArray());
    }

    private static void ValidateItems(AttemptQuestionSnapshot question, AttemptAnswerDto answer)
    {
        var items = answer.Items ?? [];
        if (answer.SelectedAnswerIds.Count > 0 || !string.IsNullOrWhiteSpace(answer.Text) || items.Any(x => x == null) ||
            items.Count > question.Options.Count || items.Select(x => x.OptionId).Distinct().Count() != items.Count ||
            items.Any(x => question.Options.All(option => option.Id != x.OptionId)))
            throw new ArgumentException("Câu trả lời từng mục không hợp lệ (mục không thuộc câu, bị trùng hoặc thừa).");
        if (question.QuestionTypeId == QuestionKinds.Matching)
        {
            var rights = question.Options.Where(x => x.MatchId != null).Select(x => x.MatchId!.Value).ToHashSet();
            if (items.Any(x => x.Value != null || x.MatchId == null || !rights.Contains(x.MatchId.Value)) ||
                items.Select(x => x.MatchId).Distinct().Count() != items.Count)
                throw new ArgumentException("Mỗi vế trái ghép với một vế phải của câu, mỗi vế phải chỉ dùng một lần.");
        }
        else if (items.Any(x => x.MatchId != null || x.Value == null))
            throw new ArgumentException("Mỗi mệnh đề cần chọn Đúng hoặc Sai.");
    }
}
