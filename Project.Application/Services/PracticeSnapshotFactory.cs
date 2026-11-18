using Project.Domain.Entities;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>
/// To-be T5: phần thuần của Luyện tập — chuẩn hóa tiêu chí, chọn ngẫu nhiên không lặp, chụp snapshot.
/// Snapshot là AttemptSnapshot v2 (ExamId/ExamDetailId = 0, điểm mỗi câu = 1, có lời giải) để dùng lại
/// ForStudent, ValidateDraft và AttemptScoring. Không truy cập DB.
/// </summary>
public static class PracticeSnapshotFactory
{
    public const int MinQuestions = 1;
    public const int MaxQuestions = 50;
    public const int MaxFilterIds = 20;
    public const int MaxOpenSessions = 3;
    public const string PracticeName = "Luyện tập";
    public const string PracticeCode = "PRACTICE";
    public static readonly string[] RevealModes = ["end", "perQuestion"];

    public sealed record Criteria(int SubjectId, IReadOnlyList<int> ChapterIds, IReadOnlyList<int> QuestionLevelIds,
        IReadOnlyList<int> TagIds, int QuestionCount, string RevealMode);

    /// <summary>Chuẩn hóa và kiểm tiêu chí; trả lỗi tiếng Việt hoặc null. Danh sách được bỏ trùng và sắp tăng.</summary>
    public static (Criteria? Value, string? Error) Normalize(int subjectId, IEnumerable<int>? chapterIds,
        IEnumerable<int>? questionLevelIds, IEnumerable<int>? tagIds, int questionCount, string? revealMode)
    {
        if (subjectId <= 0) return (null, "Môn học không hợp lệ.");
        if (questionCount is < MinQuestions or > MaxQuestions)
            return (null, $"Số câu phải từ {MinQuestions} đến {MaxQuestions}.");
        var mode = (revealMode ?? "end").Trim();
        if (!RevealModes.Contains(mode)) return (null, "Chế độ hiện đáp án chỉ nhận 'end' hoặc 'perQuestion'.");
        var lists = new List<IReadOnlyList<int>>();
        foreach (var raw in new[] { chapterIds, questionLevelIds, tagIds })
        {
            var ids = (raw ?? Array.Empty<int>()).Distinct().Order().ToArray();
            if (ids.Any(x => x <= 0)) return (null, "Bộ lọc phải là ID dương.");
            if (ids.Length > MaxFilterIds) return (null, $"Mỗi bộ lọc tối đa {MaxFilterIds} mục.");
            lists.Add(Array.AsReadOnly(ids));
        }
        return (new Criteria(subjectId, lists[0], lists[1], lists[2], questionCount, mode), null);
    }

    /// <summary>Câu dùng được cho luyện tập: cùng môn, hoạt động, loại 1–3, tập đáp án hợp lệ.</summary>
    public static bool IsUsable(Question question, int subjectId) =>
        question.SubjectId == subjectId && AttemptSnapshotFactory.QuestionShapeValid(question) &&
        AttemptSnapshotFactory.AnswerSetValid(question);

    /// <summary>Chọn <paramref name="count"/> phần tử ngẫu nhiên không lặp (Fisher–Yates một phần), thứ tự ngẫu nhiên.</summary>
    public static IReadOnlyList<T> Pick<T>(IReadOnlyList<T> pool, int count, Random random)
    {
        if (count < 0 || count > pool.Count) throw new ArgumentOutOfRangeException(nameof(count));
        var items = pool.ToArray();
        for (var i = 0; i < count; i++)
        {
            var j = random.Next(i, items.Length);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return Array.AsReadOnly(items[..count]);
    }

    /// <summary>Chụp snapshot theo đúng thứ tự câu truyền vào. Câu không dùng được → ArgumentException.</summary>
    public static AttemptSnapshot Create(int subjectId, IReadOnlyList<Question> questions)
    {
        if (questions.Count is < MinQuestions or > MaxQuestions ||
            questions.Select(x => x.Id).Distinct().Count() != questions.Count)
            throw new ArgumentException("Phiên luyện tập phải có từ 1 đến 50 câu không trùng.");
        if (questions.Any(x => !IsUsable(x, subjectId)))
            throw new ArgumentException("Câu hỏi không dùng được cho luyện tập.");
        return new AttemptSnapshot(AttemptSnapshot.CurrentVersion, 0, 0, PracticeName, PracticeCode,
            Array.AsReadOnly(questions.Select(x => AttemptSnapshotFactory.BuildQuestion(x, 1)).ToArray()));
    }
}
