using Project.Application.DTOs.AttemptDTO;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>Kết quả một câu của một lượt/phiên đã chốt (To-be T11).</summary>
public sealed record QuestionOutcomeResult(Guid SnapshotQuestionId, int SourceQuestionId, int? ChapterId,
    IReadOnlyList<int> TagIds, bool IsCorrect, bool IsAnswered, IReadOnlyList<int> SelectedSourceAnswerIds, bool FromSnapshot);

/// <summary>
/// To-be T11 (Q13, QT11-2): đúng/sai từng câu theo đúng quy tắc của <see cref="AttemptScoring"/> (tất cả-hoặc-không,
/// bỏ trống = sai) để phân tích luôn khớp điểm. Đáp án đã chọn lưu theo Id đáp án gốc (T12 thống kê đáp án nhiễu).
/// Snapshot v1/v2 không có chương/tag: dùng <paramref name="currentClassification"/> (ngân hàng lúc bù) nếu có.
/// </summary>
public static class QuestionOutcomeRules
{
    public static IReadOnlyList<QuestionOutcomeResult> Evaluate(AttemptSnapshot snapshot, IReadOnlyList<AttemptAnswerDto> savedAnswers,
        IReadOnlyDictionary<int, (int? ChapterId, IReadOnlyList<int> TagIds)>? currentClassification = null)
    {
        var answers = AttemptSnapshotFactory.ValidateDraft(snapshot, savedAnswers).ToDictionary(x => x.QuestionId);
        var fromSnapshot = snapshot.Version >= 3;
        return snapshot.Questions.Select(question =>
        {
            var selected = answers[question.Id].SelectedAnswerIds;
            var correct = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToHashSet();
            var isAnswered = selected.Count > 0;
            var isCorrect = isAnswered && selected.ToHashSet().SetEquals(correct);
            var bySnapshotId = question.Options.ToDictionary(x => x.Id, x => x.SourceAnswerId);
            var selectedSource = selected.Where(bySnapshotId.ContainsKey).Select(x => bySnapshotId[x]).Distinct().OrderBy(x => x).ToArray();
            int? chapterId; IReadOnlyList<int> tagIds;
            if (fromSnapshot) { chapterId = question.ChapterId; tagIds = question.TagIds ?? []; }
            else if (currentClassification != null && currentClassification.TryGetValue(question.SourceQuestionId, out var current))
            { chapterId = current.ChapterId; tagIds = current.TagIds; }
            else { chapterId = null; tagIds = []; }
            return new QuestionOutcomeResult(question.Id, question.SourceQuestionId, chapterId,
                tagIds.Distinct().OrderBy(x => x).ToArray(), isCorrect, isAnswered, selectedSource, fromSnapshot);
        }).ToList();
    }
}
