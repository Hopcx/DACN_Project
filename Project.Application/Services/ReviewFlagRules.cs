using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>To-be T8: quy tắc thuần cho đánh dấu câu (không DB).</summary>
public static class ReviewFlagRules
{
    public const string Exam = "exam";
    public const string Practice = "practice";

    /// <summary>Chuẩn hóa loại nguồn (không phân biệt hoa/thường, bỏ khoảng trắng); không hợp lệ → null.</summary>
    public static string? NormalizeKind(string? kind)
    {
        var value = kind?.Trim().ToLowerInvariant();
        return value is Exam or Practice ? value : null;
    }

    /// <summary>Câu có thuộc snapshot của lượt/phiên không.</summary>
    public static bool HasQuestion(AttemptSnapshot snapshot, Guid questionId) =>
        questionId != Guid.Empty && snapshot.Questions.Any(x => x.Id == questionId);

    /// <summary>Danh sách câu đã đánh dấu theo thứ tự câu trong snapshot (bỏ Guid không còn thuộc snapshot).</summary>
    public static IReadOnlyList<Guid> InSnapshotOrder(AttemptSnapshot snapshot, IEnumerable<Guid> flagged)
    {
        var set = flagged.ToHashSet();
        return Array.AsReadOnly(snapshot.Questions.Where(x => set.Contains(x.Id)).Select(x => x.Id).ToArray());
    }
}
