using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

/// <summary>
/// To-be T11 (Q13a, 3.7): phân tích riêng của học viên theo chương/tag — chỉ dữ liệu của chính người gọi (không nhận userId).
/// Gồm bài thi + luyện tập + thi thử đã chốt trong N ngày (mặc định 90). Câu bài thi chỉ tính khi lượt đã được xem kết quả
/// (chưa hủy, AllowViewResult, lịch UTC đã kết thúc — cùng điều kiện mở review) để không lộ đúng/sai trước khi công bố.
/// </summary>
[ApiController]
[Authorize(Policy = "Student")]
[Route("web/student/analysis")]
public sealed class StudentAnalysisController : ControllerBase
{
    public const int MaxRows = 20000;
    private readonly ProjectDACNDbContext _db;
    private readonly AnalysisThresholds _thresholds;
    public StudentAnalysisController(ProjectDACNDbContext db, AnalysisThresholds thresholds) { _db = db; _thresholds = thresholds; }

    private sealed record Row(int SubjectId, int? ChapterId, string TagIdsJson, bool IsCorrect, string SourceKind, string ClassificationSource);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? subjectId, [FromQuery] int days = 90, CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (days is < 1 or > 365) return BadRequest(ApiResponse<string>.Fail("INVALID_DAYS"));
        var now = DateTime.UtcNow;
        var from = now.AddDays(-days);
        var eligible = _db.QuestionOutcomes.AsNoTracking().Where(o => o.UserId == userId && o.FinishedAtUtc >= from &&
            (o.SourceKind != "exam" || _db.ExamAttempts.Any(a => a.Id == o.SourceId && a.UserId == userId && a.AllowViewResult &&
                a.Submission != null && a.Submission.VoidedAtUtc == null &&
                _db.ExamSchedules.Any(s => s.Id == a.ExamScheduleId && s.IsTimeUtc && s.EndTime <= now) &&
                // Review T11: cùng điều kiện Owned của review — còn là thành viên đã duyệt của lớp đang hoạt động
                _db.ClassExamSchedules.Any(link => link.ExamScheduleId == a.ExamScheduleId && link.Class.Status == 1 &&
                    link.Class.ClassUsers.Any(member => member.UserId == userId && member.Status == 1)))));

        var subjectCounts = await eligible.GroupBy(o => o.SubjectId).Select(g => new { SubjectId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var subjectIds = subjectCounts.Select(x => x.SubjectId).ToList();
        var subjectNames = await _db.Subjects.AsNoTracking().Where(x => subjectIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var subjects = subjectCounts.OrderBy(x => subjectNames.GetValueOrDefault(x.SubjectId)).ThenBy(x => x.SubjectId)
            .Select(x => new { id = x.SubjectId, name = subjectNames.GetValueOrDefault(x.SubjectId) ?? $"#{x.SubjectId}", questionCount = x.Count })
            .ToList();
        var selected = subjectId ?? subjects.FirstOrDefault()?.id;
        var thresholds = new { _thresholds.WeakBelow, _thresholds.ImproveBelow, _thresholds.GoodFrom, _thresholds.MinQuestions };
        if (selected == null || !subjectIds.Contains(selected.Value))
            return Ok(ApiResponse<object>.Ok(new { days, from = from, serverNow = now, subjects, subjectId = selected, thresholds,
                totals = (object?)null, chapters = Array.Empty<object>(), tags = Array.Empty<object>() }));

        // Review T11: vượt giới hạn thì giữ các câu gần nhất (thứ tự cố định)
        var rows = await eligible.Where(o => o.SubjectId == selected.Value)
            .OrderByDescending(o => o.FinishedAtUtc).ThenBy(o => o.SourceId).ThenBy(o => o.SnapshotQuestionId)
            .Select(o => new Row(o.SubjectId, o.ChapterId, o.TagIdsJson, o.IsCorrect, o.SourceKind, o.ClassificationSource))
            .Take(MaxRows + 1).ToListAsync(ct);
        var truncated = rows.Count > MaxRows;
        if (truncated) rows = rows.Take(MaxRows).ToList();

        var tagRows = rows.SelectMany(r => (JsonSerializer.Deserialize<int[]>(r.TagIdsJson) ?? []).Distinct().Select(t => (TagId: t, r.IsCorrect))).ToList();
        var chapterIds = rows.Where(r => r.ChapterId != null).Select(r => r.ChapterId!.Value).Distinct().ToList();
        var tagIds = tagRows.Select(x => x.TagId).Distinct().ToList();
        var chapterInfo = await _db.Chapters.AsNoTracking().Where(x => chapterIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Status, x.SortOrder }).ToDictionaryAsync(x => x.Id, ct);
        var tagInfo = await _db.Tags.AsNoTracking().Where(x => tagIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Status }).ToDictionaryAsync(x => x.Id, ct);

        object Group(int total, int correct) => new
        {
            total, correct, rate = AnalysisRules.Rate(correct, total), level = AnalysisRules.Classify(correct, total, _thresholds)
        };
        var chapters = rows.Where(r => r.ChapterId != null).GroupBy(r => r.ChapterId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Count(), Correct = g.Count(x => x.IsCorrect), Info = chapterInfo.GetValueOrDefault(g.Key) })
            .OrderBy(x => x.Info?.SortOrder ?? int.MaxValue).ThenBy(x => x.Info?.Name).ThenBy(x => x.Id)
            .Select(x => new
            {
                chapterId = x.Id, name = x.Info?.Name ?? $"#{x.Id}", hidden = x.Info == null || x.Info.Status != 1,
                stats = Group(x.Total, x.Correct)
            }).ToList();
        var tags = tagRows.GroupBy(x => x.TagId)
            .Select(g => new { Id = g.Key, Total = g.Count(), Correct = g.Count(x => x.IsCorrect), Info = tagInfo.GetValueOrDefault(g.Key) })
            .OrderBy(x => x.Info?.Name).ThenBy(x => x.Id)
            .Select(x => new { tagId = x.Id, name = x.Info?.Name ?? $"#{x.Id}", hidden = x.Info == null || x.Info.Status != 1,
                stats = Group(x.Total, x.Correct) }).ToList();
        var unchaptered = rows.Where(r => r.ChapterId == null).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            days, from = from, serverNow = now, subjects, subjectId = selected, thresholds, truncated,
            totals = Group(rows.Count, rows.Count(r => r.IsCorrect)),
            bySource = new
            {
                exam = rows.Count(r => r.SourceKind == "exam"), practice = rows.Count(r => r.SourceKind == "practice"),
                mock = rows.Count(r => r.SourceKind == "mock")
            },
            currentClassificationCount = rows.Count(r => r.ClassificationSource == "current"),
            unchaptered = Group(unchaptered.Count, unchaptered.Count(r => r.IsCorrect)),
            chapters, tags
        }));
    }
}
