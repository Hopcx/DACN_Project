using Project.Application.DTOs.AttemptDTO;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>Lựa chọn của câu đã chốt bị thay đổi khi lưu nháp (To-be T6, QT6-3 a).</summary>
public sealed class PracticeQuestionLockedException : Exception
{
    public PracticeQuestionLockedException() : base("PRACTICE_QUESTION_LOCKED") { }
}

/// <summary>To-be T16: câu điền ngắn có Text (bài làm) và AcceptedAnswers (đáp án chấp nhận); loại 1–3 để null.</summary>
public sealed record PracticeCheckResult(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds, bool IsCorrect,
    IReadOnlyList<Guid> CorrectAnswerIds, string? Explanation, string? Text = null, IReadOnlyList<string>? AcceptedAnswers = null);

/// <summary>
/// To-be T6: chế độ "kiểm tra từng câu" — hàm thuần, không DB. Câu đã chốt giữ nguyên lựa chọn đến khi nộp;
/// kết quả câu đã chốt tính lại từ snapshot + nháp (không lưu đáp án ở chỗ khác).
/// </summary>
public static class PracticeCheck
{
    /// <summary>Đúng khi chọn đủ và chỉ các đáp án đúng (tất cả-hoặc-không, Q3).</summary>
    public static PracticeCheckResult Result(AttemptQuestionSnapshot question, IReadOnlyList<Guid> selected) =>
        Result(question, new AttemptAnswerDto(question.Id, selected));

    /// <summary>T16: cùng quy tắc với điểm (<see cref="AnswerGrading"/>), gồm câu điền ngắn.</summary>
    public static PracticeCheckResult Result(AttemptQuestionSnapshot question, AttemptAnswerDto answer)
    {
        var shortAnswer = AnswerGrading.IsShortAnswer(question);
        var correct = shortAnswer ? Array.Empty<Guid>() : question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToArray();
        return new PracticeCheckResult(question.Id, answer.SelectedAnswerIds, AnswerGrading.IsCorrect(question, answer),
            Array.AsReadOnly(correct), question.Explanation,
            shortAnswer ? answer.Text : null, shortAnswer ? AnswerGrading.AcceptedAnswers(question) : null);
    }

    /// <summary>Kết quả các câu đã chốt theo thứ tự snapshot (cho GET phiên perQuestion).</summary>
    public static IReadOnlyList<PracticeCheckResult> Results(AttemptSnapshot snapshot, IReadOnlyList<AttemptAnswerDto> draft,
        IReadOnlyCollection<Guid> checkedIds)
    {
        var answers = draft.ToDictionary(x => x.QuestionId);
        return Array.AsReadOnly(snapshot.Questions.Where(x => checkedIds.Contains(x.Id))
            .Select(x => Result(x, answers.TryGetValue(x.Id, out var a) ? a : new AttemptAnswerDto(x.Id, Array.Empty<Guid>()))).ToArray());
    }

    /// <summary>
    /// Chốt một câu với lựa chọn gửi kèm: thay lựa chọn của câu đó trong nháp (kiểm bằng ValidateDraft), thêm vào danh sách chốt.
    /// Câu không thuộc phiên / lựa chọn sai / trống → ArgumentException. Câu đã chốt → không gọi hàm này (trả kết quả cũ).
    /// T16: câu điền ngắn gửi <paramref name="text"/> (không chọn đáp án).
    /// </summary>
    public static (IReadOnlyList<AttemptAnswerDto> Draft, IReadOnlyList<Guid> Checked) Apply(AttemptSnapshot snapshot,
        IReadOnlyList<AttemptAnswerDto> draft, IReadOnlyList<Guid> checkedIds, Guid questionId, IReadOnlyList<Guid>? selected,
        string? text = null)
    {
        var question = snapshot.Questions.FirstOrDefault(x => x.Id == questionId) ?? throw new ArgumentException("Câu hỏi không thuộc phiên.");
        if (checkedIds.Contains(questionId)) throw new ArgumentException("Câu hỏi đã được kiểm tra.");
        var answer = new AttemptAnswerDto(questionId, selected ?? Array.Empty<Guid>(), text);
        if (!AnswerGrading.IsAnswered(question, answer))
            throw new ArgumentException(AnswerGrading.IsShortAnswer(question) ? "Hãy nhập câu trả lời trước khi kiểm tra." : "Hãy chọn đáp án trước khi kiểm tra.");
        var next = draft.Where(x => x.QuestionId != questionId && (x.SelectedAnswerIds.Count > 0 || !string.IsNullOrWhiteSpace(x.Text))).ToList();
        next.Add(answer);
        var validated = AttemptSnapshotFactory.ValidateDraft(snapshot, next);
        return (validated, Array.AsReadOnly(checkedIds.Append(questionId).ToArray()));
    }

    /// <summary>
    /// Lưu nháp có câu đã chốt (thay toàn bộ như T5): câu đã chốt bị bỏ qua trong payload → giữ lựa chọn cũ;
    /// payload gửi lựa chọn khác → PracticeQuestionLockedException. Lỗi lựa chọn khác → ArgumentException.
    /// </summary>
    public static IReadOnlyList<AttemptAnswerDto> Merge(AttemptSnapshot snapshot, IReadOnlyList<AttemptAnswerDto> current,
        IReadOnlyCollection<Guid> checkedIds, IReadOnlyList<AttemptAnswerDto>? incoming)
    {
        var validated = AttemptSnapshotFactory.ValidateDraft(snapshot, incoming);
        if (checkedIds.Count == 0) return validated;
        var locked = current.Where(x => checkedIds.Contains(x.QuestionId)).ToDictionary(x => x.QuestionId);
        var canonical = validated.ToDictionary(x => x.QuestionId);
        foreach (var answer in incoming!)
            if (locked.TryGetValue(answer.QuestionId, out var kept) &&
                (!canonical[answer.QuestionId].SelectedAnswerIds.ToHashSet().SetEquals(kept.SelectedAnswerIds) ||
                 (canonical[answer.QuestionId].Text ?? "") != (kept.Text ?? "")))
                throw new PracticeQuestionLockedException();
        return Array.AsReadOnly(validated.Select(x => locked.TryGetValue(x.QuestionId, out var kept) ? kept : x).ToArray());
    }
}
