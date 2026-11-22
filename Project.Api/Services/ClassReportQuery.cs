using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>
/// To-be T12 (Q13b): báo cáo lớp cho giảng viên/Admin — chỉ bài thi. Dùng cùng lịch, thành viên và lượt được tính của
/// xếp hạng T10 (QT12-1/QT12-2), đọc kết quả từng câu T11 (<c>QuestionOutcomes</c>) của các lượt đó.
/// Thông tin câu/đáp án nhiễu theo ngân hàng hiện tại; KHÔNG trả đáp án đúng.
/// </summary>
public sealed class ClassReportQuery
{
    public const int ContentMaxLength = 200;
    private readonly ProjectDACNDbContext _db;
    private readonly ClassRankingQuery _ranking;
    private readonly AnalysisThresholds _thresholds;

    public ClassReportQuery(ProjectDACNDbContext db, ClassRankingQuery ranking, AnalysisThresholds thresholds)
    { _db = db; _ranking = ranking; _thresholds = thresholds; }

    public static string Shorten(string? text, int max = ContentMaxLength)
    {
        var value = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        if (value.Length <= max) return value;
        // Review T12: không cắt giữa cặp surrogate (emoji, ký tự ngoài BMP)
        var cut = max - 1;
        if (char.IsLowSurrogate(value[cut])) cut--;
        return value[..cut].TrimEnd() + "…";
    }

    /// <returns>null khi lớp vượt giới hạn của <see cref="ClassRankingQuery"/>.</returns>
    public async Task<ClassReportView?> LoadAsync(int classId, CancellationToken ct = default)
    {
        var data = await _ranking.LoadAsync(classId, ct);
        if (data == null) return null;
        var chosen = data.ChosenAttempts.Values.Distinct().ToList();
        var rows = chosen.Count == 0 ? []
            : await _db.QuestionOutcomes.AsNoTracking().Where(o => o.SourceKind == "exam" && chosen.Contains(o.SourceId))
                .Select(o => new { o.SourceId, o.UserId, o.SourceQuestionId, o.ChapterId, o.IsCorrect, o.SelectedAnswerIdsJson, o.ClassificationSource })
                .ToListAsync(ct);
        var outcomes = rows.Select(o => new ReportOutcome(o.UserId, o.SourceQuestionId, o.ChapterId, o.IsCorrect,
            JsonSerializer.Deserialize<int[]>(o.SelectedAnswerIdsJson) ?? [])).ToList();
        // Review T12: đáp án đúng theo SNAPSHOT lúc thi (không theo ngân hàng hiện tại), đọc bằng OPENJSON trong SQL để không
        // kéo SnapshotJson về API. Chỉ dùng nội bộ để loại đáp án đúng khỏi "đáp án nhiễu"; không trả ra ngoài.
        var correctIds = chosen.Count == 0 ? new HashSet<int>()
            : (await _db.Database.SqlQuery<int>($"""
                SELECT DISTINCT o.sourceAnswerId AS Value
                FROM dbo.ExamAttempts a
                CROSS APPLY OPENJSON(CASE WHEN ISJSON(a.SnapshotJson) = 1 THEN a.SnapshotJson ELSE NULL END, '$.questions')
                    WITH (options nvarchar(max) '$.options' AS JSON) q
                CROSS APPLY OPENJSON(q.options) WITH (sourceAnswerId int '$.sourceAnswerId', isCorrect bit '$.isCorrect') o
                WHERE a.Id IN (SELECT CAST(value AS uniqueidentifier) FROM OPENJSON({JsonSerializer.Serialize(chosen)}))
                  AND o.isCorrect = 1 AND o.sourceAnswerId IS NOT NULL
                """).ToListAsync(ct)).ToHashSet();
        var report = ClassReport.Build(outcomes, _thresholds, correctIds);

        var chapterIds = report.Chapters.Where(c => c.ChapterId != null).Select(c => c.ChapterId!.Value).ToList();
        var hardIds = report.HardQuestions.Select(h => h.SourceQuestionId).ToList();
        var distractorIds = report.HardQuestions.Where(h => h.DistractorAnswerId != null).Select(h => h.DistractorAnswerId!.Value).ToList();
        var questions = await _db.Questions.AsNoTracking().Where(q => hardIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Content, q.Status, Level = q.QuestionLevel != null ? q.QuestionLevel.Name : null })
            .ToDictionaryAsync(q => q.Id, ct);
        chapterIds.AddRange(report.HardQuestions.Where(h => h.ChapterId != null).Select(h => h.ChapterId!.Value));
        var chapterInfo = await _db.Chapters.AsNoTracking().Where(c => chapterIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Status, c.SortOrder }).ToDictionaryAsync(c => c.Id, ct);
        var distractors = await _db.Answers.AsNoTracking().Where(a => distractorIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Content }).ToDictionaryAsync(a => a.Id, a => a.Content, ct);
        var names = data.Members.ToDictionary(m => m.UserId);

        var counted = data.Schedules.Where(s => s.Counted).ToList();
        return new ClassReportView("classExamSchedulesOnly", "rankingAttempt", "snapshotOrCurrentBank", "snapshot", true, data.ServerNow,
            new ReportThresholdsView(_thresholds.WeakBelow, _thresholds.MinQuestions, _thresholds.HardBelow, _thresholds.HardMinAnswers,
                ClassReport.MaxHardQuestions),
            counted.Count, data.Schedules.Count - counted.Count, chosen.Count, chosen.Count - rows.Select(r => r.SourceId).Distinct().Count(),
            outcomes.Select(o => o.UserId).Distinct().Count(), rows.Count(r => r.ClassificationSource == "current"),
            report.QuestionCount, report.MaxAnswersPerQuestion,
            report.Chapters
                .OrderBy(c => c.ChapterId == null).ThenBy(c => c.ChapterId == null ? 0 : chapterInfo.GetValueOrDefault(c.ChapterId.Value)?.SortOrder ?? int.MaxValue)
                .ThenBy(c => c.ChapterId == null ? "" : chapterInfo.GetValueOrDefault(c.ChapterId.Value)?.Name).ThenBy(c => c.ChapterId)
                .Select(c =>
                {
                    var info = c.ChapterId == null ? null : chapterInfo.GetValueOrDefault(c.ChapterId.Value);
                    return new ReportChapterView(c.ChapterId, c.ChapterId == null ? null : info?.Name ?? $"#{c.ChapterId}",
                        c.ChapterId != null && (info == null || info.Status != 1), c.Total, c.Correct, c.Rate, c.StudentCount,
                        c.WeakStudents.Count,
                        c.WeakStudents.Select(id => names.TryGetValue(id, out var m)
                                ? new ReportWeakStudent(id, m.FullName, m.UserName) : new ReportWeakStudent(id, "?", "?"))
                            .OrderBy(x => x.FullName).ToList());
                }).ToList(),
            report.HardQuestions.Select(h =>
            {
                var q = questions.GetValueOrDefault(h.SourceQuestionId);
                var chapter = h.ChapterId == null ? null : chapterInfo.GetValueOrDefault(h.ChapterId.Value);
                return new ReportHardView(h.SourceQuestionId, q == null ? "(câu đã xóa)" : Shorten(q.Content), q == null || q.Status != 1,
                    chapter?.Name, q?.Level, h.Answers, h.Correct, h.Rate,
                    h.DistractorAnswerId == null ? null : Shorten(distractors.GetValueOrDefault(h.DistractorAnswerId.Value) ?? "(đáp án đã xóa)"),
                    h.DistractorCount);
            }).ToList())
        { Ranking = data };
    }
}

// To-be T13: model có kiểu của báo cáo lớp — dùng chung cho JSON (endpoint analysis, giữ nguyên tên trường camelCase) và Excel.
public sealed record ReportWeakStudent(Guid UserId, string FullName, string UserName);

public sealed record ReportChapterView(int? ChapterId, string? Name, bool Hidden, int Total, int Correct, decimal? Rate, int StudentCount,
    int WeakCount, IReadOnlyList<ReportWeakStudent> WeakStudents);

public sealed record ReportHardView(int QuestionId, string Content, bool Hidden, string? ChapterName, string? Level, int Answers,
    int Correct, decimal? Rate, string? Distractor, int DistractorCount);

public sealed record ReportThresholdsView(decimal WeakBelow, int MinQuestions, decimal HardBelow, int HardMinAnswers, int MaxHardQuestions);

public sealed record ClassReportView(string Scope, string AttemptsCounted, string Classification, string CorrectAnswersFrom,
    bool AnswersIncludeBlank, DateTime ServerNow, ReportThresholdsView Thresholds, int ScheduleCount, int NotCountedCount,
    int AttemptCount, int AttemptsWithoutOutcomes, int StudentCount, int CurrentClassificationCount, int QuestionCount,
    int MaxAnswersPerQuestion, IReadOnlyList<ReportChapterView> Chapters, IReadOnlyList<ReportHardView> HardQuestions)
{
    /// <summary>Dữ liệu xếp hạng dùng để dựng báo cáo (cho Excel); không đưa vào JSON.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ClassRankingData Ranking { get; init; } = null!;
}
