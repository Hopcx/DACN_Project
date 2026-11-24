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
    // To-be T9: Thi thử (Kind "mock") — có giới hạn giờ, chỉ hiện đáp án sau khi nộp, tối đa 1 phiên đang làm
    public const string KindPractice = "practice";
    public const string KindMock = "mock";
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 180;
    public const int MaxOpenMocks = 1;

    public sealed record Criteria(int SubjectId, IReadOnlyList<int> ChapterIds, IReadOnlyList<int> QuestionLevelIds,
        IReadOnlyList<int> TagIds, int QuestionCount, string RevealMode, string Kind = KindPractice, int? DurationMinutes = null);

    /// <summary>Chuẩn hóa và kiểm tiêu chí; trả lỗi tiếng Việt hoặc null. Danh sách được bỏ trùng và sắp tăng.</summary>
    public static (Criteria? Value, string? Error) Normalize(int subjectId, IEnumerable<int>? chapterIds,
        IEnumerable<int>? questionLevelIds, IEnumerable<int>? tagIds, int questionCount, string? revealMode,
        string? kind = null, int? durationMinutes = null)
    {
        var sessionKind = (kind ?? KindPractice).Trim().ToLowerInvariant();
        if (sessionKind is not (KindPractice or KindMock)) return (null, "Loại phiên chỉ nhận 'practice' hoặc 'mock'.");
        if (subjectId <= 0) return (null, "Môn học không hợp lệ.");
        if (questionCount is < MinQuestions or > MaxQuestions)
            return (null, $"Số câu phải từ {MinQuestions} đến {MaxQuestions}.");
        var mode = (revealMode ?? "end").Trim();
        if (!RevealModes.Contains(mode)) return (null, "Chế độ hiện đáp án chỉ nhận 'end' hoặc 'perQuestion'.");
        if (sessionKind == KindMock)
        {
            if (mode != "end") return (null, "Thi thử chỉ hiện đáp án sau khi nộp bài.");
            if (durationMinutes is not (>= MinDurationMinutes and <= MaxDurationMinutes))
                return (null, $"Thời lượng thi thử phải từ {MinDurationMinutes} đến {MaxDurationMinutes} phút.");
        }
        else durationMinutes = null;
        var lists = new List<IReadOnlyList<int>>();
        foreach (var raw in new[] { chapterIds, questionLevelIds, tagIds })
        {
            var ids = (raw ?? Array.Empty<int>()).Distinct().Order().ToArray();
            if (ids.Any(x => x <= 0)) return (null, "Bộ lọc phải là ID dương.");
            if (ids.Length > MaxFilterIds) return (null, $"Mỗi bộ lọc tối đa {MaxFilterIds} mục.");
            lists.Add(Array.AsReadOnly(ids));
        }
        return (new Criteria(subjectId, lists[0], lists[1], lists[2], questionCount, mode, sessionKind, durationMinutes), null);
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
        // T18: số câu thực tế có thể vượt số yêu cầu để không cắt đoạn dùng chung (tối đa GroupedSelection.MaxPracticeQuestions)
        if (questions.Count is < MinQuestions or > GroupedSelection.MaxPracticeQuestions ||
            questions.Select(x => x.Id).Distinct().Count() != questions.Count)
            throw new ArgumentException($"Phiên luyện tập phải có từ 1 đến {GroupedSelection.MaxPracticeQuestions} câu không trùng.");
        if (questions.Any(x => !IsUsable(x, subjectId)))
            throw new ArgumentException("Câu hỏi không dùng được cho luyện tập.");
        var built = Array.AsReadOnly(questions.Select(x => AttemptSnapshotFactory.BuildQuestion(x, 1)).ToArray());
        var (grouped, groups) = AttemptSnapshotFactory.WithGroups(questions, built);
        return new AttemptSnapshot(AttemptSnapshot.CurrentVersion, 0, 0, PracticeName, PracticeCode, grouped, groups);
    }
}
