using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "Student")]
[Route("web/student/classes")]
public class StudentClassController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly ClassMembershipStore _membership;
    private readonly ClassRankingQuery _ranking;
    public StudentClassController(ProjectDACNDbContext db, ClassMembershipStore membership, ClassRankingQuery ranking)
    { _db = db; _membership = membership; _ranking = ranking; }

    // To-be T10 (Q8d): học viên chỉ thấy điểm và hạng của mình. Không trả tên/điểm/nhóm của bạn khác, không nhãn
    // Top/Bottom. Hạng chỉ hiện khi lớp có ≥ 5 học viên có điểm. Không phải thành viên đã duyệt của lớp đang hoạt động → 404.
    [HttpGet("{classId:int}/ranking")]
    public async Task<IActionResult> Ranking(int classId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await _db.ClassUsers.AnyAsync(x => x.ClassId == classId && x.UserId == userId && x.Status == 1 &&
                x.Class.Status == 1, ct))
            return NotFound(ApiResponse<string>.Fail("CLASS_NOT_FOUND"));
        var data = await _ranking.LoadAsync(classId, ct);
        if (data == null) return UnprocessableEntity(ApiResponse<string>.Fail("CLASS_TOO_LARGE"));
        // Review T10: học viên có thể rời lớp giữa lúc kiểm quyền và lúc đọc → 404, không 500.
        var me = data.Result.Rows.SingleOrDefault(x => x.UserId == userId);
        if (me == null) return NotFound(ApiResponse<string>.Fail("CLASS_NOT_FOUND"));
        var counted = data.Schedules.Where(x => x.Counted).ToList();
        var visible = data.Result.RankingVisible;
        return Ok(ApiResponse<object>.Ok(new
        {
            scope = "classExamSchedulesOnly", scale = 10, scoreMethod = "highestAttempt", absentScore = 0,
            minScoredForRanking = ClassRanking.MinScoredForRanking, scoredCount = data.Result.ScoredCount,
            serverNow = data.ServerNow,
            total = me.Total, attemptedCount = me.AttemptedCount, scheduleCount = counted.Count, passedCount = me.PassedCount,
            rank = visible ? me.Rank : null,
            rankHiddenReason = me.Total == null ? "NO_SCORE" : visible ? null : "CLASS_TOO_SMALL",
            schedules = counted.Select(s => new
                { id = s.Id, title = s.Title, examName = s.ExamName, endTime = s.EndTime, score = me.Scores[s.Id] }),
            notCountedCount = data.Schedules.Count - counted.Count
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var rows = await _db.ClassUsers.AsNoTracking()
            .Where(x => x.UserId == userId && x.Class.Status == 1 && (x.Status == 1 || x.Status == 2))
            .OrderBy(x => x.Class.Name).ThenBy(x => x.Id)
            .Select(x => new StudentClassDto(x.Id, x.ClassId, x.Class.Name, x.Class.ClassCode,
                x.Class.Description, x.Class.Capacity, x.Class.Subject != null ? x.Class.Subject.Name : null, x.Status))
            .ToListAsync();
        return Ok(ApiResponse<List<StudentClassDto>>.Ok(rows));
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinClassRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var code = request.ClassCode.Trim();
        var classId = await _db.Classes.AsNoTracking().Where(x => x.ClassCode == code)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (classId == null) return NotFound(ApiResponse<string>.Fail("Mã lớp không tồn tại"));
        var result = await _membership.JoinAsync(classId.Value, userId, false, code);
        return result.Outcome switch
        {
            MembershipOutcome.Created => Created("", ApiResponse<object>.Ok(new { classId, status = 2 }, "Đã gửi yêu cầu tham gia")),
            MembershipOutcome.Duplicate => Conflict(ApiResponse<string>.Fail("Bạn đã tham gia hoặc đang chờ duyệt")),
            MembershipOutcome.Full => Conflict(ApiResponse<string>.Fail("Lớp đã đủ sĩ số")),
            MembershipOutcome.InactiveClass => Conflict(ApiResponse<string>.Fail("Lớp không hoạt động")),
            _ => BadRequest(ApiResponse<string>.Fail("Không thể tham gia lớp"))
        };
    }

    [HttpDelete("{classId:int}")]
    public async Task<IActionResult> Leave(int classId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await _membership.RemoveAsync(classId, userId);
        return result.Outcome == MembershipOutcome.Removed
            ? Ok(ApiResponse<string>.Ok("Đã rời lớp"))
            : NotFound(ApiResponse<string>.Fail("Bạn không ở trong lớp này"));
    }
}

public record StudentClassDto(int MembershipId, int ClassId, string Name, string ClassCode,
    string? Description, int Capacity, string? SubjectName, byte? Status);

public class JoinClassRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string ClassCode { get; set; } = string.Empty;
}
