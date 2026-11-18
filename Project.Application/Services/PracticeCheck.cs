using Project.Application.DTOs.AttemptDTO;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>Lựa chọn của câu đã chốt bị thay đổi khi lưu nháp (To-be T6, QT6-3 a).</summary>
public sealed class PracticeQuestionLockedException : Exception
{
    public PracticeQuestionLockedException() : base("PRACTICE_QUESTION_LOCKED") { }
}

public sealed record PracticeCheckResult(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds, bool IsCorrect,
    IReadOnlyList<Guid> CorrectAnswerIds, string? Explanation);

/// <summary>
/// To-be T6: chế độ "kiểm tra từng câu" — hàm thuần, không DB. Câu đã chốt giữ nguyên lựa chọn đến khi nộp;
/// kết quả câu đã chốt tính lại từ snapshot + nháp (không lưu đáp án ở chỗ khác).
/// </summary>
public static class PracticeCheck
{
    /// <summary>Đúng khi chọn đủ và chỉ các đáp án đúng (tất cả-hoặc-không, Q3).</summary>
    public static PracticeCheckResult Result(AttemptQuestionSnapshot question, IReadOnlyList<Guid> selected)
    {
        var correct = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToArray();
        return new PracticeCheckResult(question.Id, selected, selected.Count > 0 && selected.ToHashSet().SetEquals(correct),
            Array.AsReadOnly(correct), question.Explanation);
    }

    /// <summary>Kết quả các câu đã chốt theo thứ tự snapshot (cho GET phiên perQuestion).</summary>
    public static IReadOnlyList<PracticeCheckResult> Results(AttemptSnapshot snapshot, IReadOnlyList<AttemptAnswerDto> draft,
        IReadOnlyCollection<Guid> checkedIds)
    {
        var selected = draft.ToDictionary(x => x.QuestionId, x => x.SelectedAnswerIds);
        return Array.AsReadOnly(snapshot.Questions.Where(x => checkedIds.Contains(x.Id))
            .Select(x => Result(x, selected.TryGetValue(x.Id, out var ids) ? ids : Array.Empty<Guid>())).ToArray());
    }

    /// <summary>
    /// Chốt một câu với lựa chọn gửi kèm: thay lựa chọn của câu đó trong nháp (kiểm bằng ValidateDraft), thêm vào danh sách chốt.
    /// Câu không thuộc phiên / lựa chọn sai / trống → ArgumentException. Câu đã chốt → không gọi hàm này (trả kết quả cũ).
    /// </summary>
    public static (IReadOnlyList<AttemptAnswerDto> Draft, IReadOnlyList<Guid> Checked) Apply(AttemptSnapshot snapshot,
        IReadOnlyList<AttemptAnswerDto> draft, IReadOnlyList<Guid> checkedIds, Guid questionId, IReadOnlyList<Guid>? selected)
    {
        if (snapshot.Questions.All(x => x.Id != questionId)) throw new ArgumentException("Câu hỏi không thuộc phiên.");
        if (checkedIds.Contains(questionId)) throw new ArgumentException("Câu hỏi đã được kiểm tra.");
        if (selected == null || selected.Count == 0) throw new ArgumentException("Hãy chọn đáp án trước khi kiểm tra.");
        var next = draft.Where(x => x.QuestionId != questionId && x.SelectedAnswerIds.Count > 0).ToList();
        next.Add(new AttemptAnswerDto(questionId, selected));
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
        var locked = current.Where(x => checkedIds.Contains(x.QuestionId)).ToDictionary(x => x.QuestionId, x => x.SelectedAnswerIds);
        foreach (var answer in incoming!)
            if (locked.TryGetValue(answer.QuestionId, out var kept) && !answer.SelectedAnswerIds.ToHashSet().SetEquals(kept))
                throw new PracticeQuestionLockedException();
        return Array.AsReadOnly(validated.Select(x => locked.TryGetValue(x.QuestionId, out var kept)
            ? new AttemptAnswerDto(x.QuestionId, kept) : x).ToArray());
    }
}
