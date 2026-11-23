using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Application.DTOs.AttemptDTO;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>
/// To-be T11: thêm dòng <see cref="QuestionOutcome"/> cho một nguồn vừa chốt (chưa SaveChanges — người gọi lưu cùng
/// transaction chốt). Idempotent: nguồn đã có dòng thì không thêm. Dùng chung cho hai finalizer và worker bù.
/// </summary>
public static class QuestionOutcomeWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<int> AddAsync(ProjectDACNDbContext db, string kind, Guid sourceId, Guid userId, int subjectId,
        int? examScheduleId, DateTime finishedAtUtc, string snapshotJson, string answersJson, CancellationToken ct = default)
    {
        if (await db.QuestionOutcomes.AnyAsync(x => x.SourceKind == kind && x.SourceId == sourceId, ct)) return 0;
        var snapshot = JsonSerializer.Deserialize<AttemptSnapshot>(snapshotJson, Json)
            ?? throw new InvalidOperationException("Snapshot is missing.");
        var answers = JsonSerializer.Deserialize<List<AttemptAnswerDto>>(answersJson, Json) ?? [];
        IReadOnlyDictionary<int, (int? ChapterId, IReadOnlyList<int> TagIds)>? current = null;
        if (snapshot.Version < 3)
        {
            // v1/v2: phân loại theo ngân hàng hiện tại (QT11/Q13d), ghi ClassificationSource = "current"
            var ids = snapshot.Questions.Select(x => x.SourceQuestionId).Distinct().ToList();
            var rows = await db.Questions.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, x.ChapterId, Tags = x.QuestionTags.Select(t => t.TagId).ToList() }).ToListAsync(ct);
            current = rows.ToDictionary(x => x.Id, x => (x.ChapterId, (IReadOnlyList<int>)x.Tags));
        }
        var results = QuestionOutcomeRules.Evaluate(snapshot, answers, current);
        var finished = DateTime.SpecifyKind(finishedAtUtc, DateTimeKind.Utc);
        db.QuestionOutcomes.AddRange(results.Select(r => new QuestionOutcome
        {
            SourceKind = kind, SourceId = sourceId, SnapshotQuestionId = r.SnapshotQuestionId, UserId = userId,
            SubjectId = subjectId, ExamScheduleId = examScheduleId, SourceQuestionId = r.SourceQuestionId,
            ChapterId = r.ChapterId, TagIdsJson = JsonSerializer.Serialize(r.TagIds), IsCorrect = r.IsCorrect,
            IsAnswered = r.IsAnswered, SelectedAnswerIdsJson = JsonSerializer.Serialize(r.SelectedSourceAnswerIds),
            AnswerText = r.AnswerText,   // To-be T16 (đã giới hạn 200 ký tự trong ShortAnswerMatcher.StatsKey)
            FinishedAtUtc = finished, ClassificationSource = r.FromSnapshot ? "snapshot" : "current"
        }));
        return results.Count;
    }

    public static Task<int> AddForAttemptAsync(ProjectDACNDbContext db, ExamAttempt row, int subjectId, CancellationToken ct = default) =>
        AddAsync(db, "exam", row.Id, row.UserId, subjectId, row.ExamScheduleId,
            row.Submission!.SubmittedAtUtc, row.SnapshotJson, row.Submission.AnswersJson, ct);

    public static Task<int> AddForPracticeAsync(ProjectDACNDbContext db, PracticeSession row, CancellationToken ct = default) =>
        AddAsync(db, row.Kind == "mock" ? "mock" : "practice", row.Id, row.UserId, row.SubjectId, null,
            row.FinishedAtUtc!.Value, row.SnapshotJson, row.DraftJson, ct);
}
