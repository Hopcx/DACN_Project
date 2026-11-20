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
[Authorize(Policy = "Teacher")]
[Route("web/teacher/classes")]
public class TeacherClassController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly ClassMembershipStore _membership;
    private readonly ClassRankingQuery _ranking;

    public TeacherClassController(ProjectDACNDbContext db, ClassMembershipStore membership, ClassRankingQuery ranking)
    { _db = db; _membership = membership; _ranking = ranking; }

    private bool TryTeacher(out Guid teacherId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out teacherId);

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        var rows = await _db.Classes.AsNoTracking().Where(x => IsAdmin || x.TeacherId == teacherId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.ClassCode, x.Description, x.Capacity,
                x.SubjectId, SubjectName = x.Subject != null ? x.Subject.Name : null, x.Status })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows));
    }

    [HttpGet("{classId:int}/members")]
    public async Task<IActionResult> Members(int classId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var rows = await _db.ClassUsers.AsNoTracking().Where(x => x.ClassId == classId)
            .OrderBy(x => x.Status).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ClassId, x.UserId, x.Status,
                x.User.FullName, x.User.Email })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows));
    }

    // Exact email only, scoped to a class owned by this teacher. No global directory.
    [HttpGet("{classId:int}/students/lookup")]
    public async Task<IActionResult> Lookup(int classId, [FromQuery] string email)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !new EmailAddressAttribute().IsValid(normalized))
            return BadRequest(ApiResponse<string>.Fail("Email không hợp lệ"));
        var student = await _db.Users.AsNoTracking()
            .Where(x => x.Email == normalized && x.LevelId == 4 && x.Status == 1 && x.EmailVerifiedAt != null)
            .Select(x => new { x.Id, x.FullName, x.Email }).FirstOrDefaultAsync();
        return student == null ? NotFound(ApiResponse<string>.Fail("Không tìm thấy học viên đã xác minh email"))
            : Ok(ApiResponse<object>.Ok(student));
    }

    [HttpPost("{classId:int}/members")]
    public async Task<IActionResult> Add(int classId, [FromBody] TeacherAddStudentRequest request)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var normalized = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !new EmailAddressAttribute().IsValid(normalized))
            return BadRequest(ApiResponse<string>.Fail("Email không hợp lệ"));
        var studentId = await _db.Users.AsNoTracking()
            .Where(x => x.Email == normalized && x.LevelId == 4 && x.Status == 1 && x.EmailVerifiedAt != null)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (studentId == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy học viên đã xác minh email"));
        return WriteResult(await _membership.JoinAsync(classId, studentId.Value, true, teacherId: Scope(teacherId)), true);
    }

    [HttpPost("{classId:int}/members/{membershipId:int}/approve")]
    public async Task<IActionResult> Approve(int classId, int membershipId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        if (!await _db.ClassUsers.AnyAsync(x => x.Id == membershipId && x.ClassId == classId))
            return NotFound(ApiResponse<string>.Fail("Thành viên không tồn tại"));
        return WriteResult(await _membership.ApproveAsync(membershipId, Scope(teacherId)), false);
    }

    [HttpDelete("{classId:int}/members/{membershipId:int}")]
    public async Task<IActionResult> Remove(int classId, int membershipId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var studentId = await _db.ClassUsers.AsNoTracking()
            .Where(x => x.Id == membershipId && x.ClassId == classId)
            .Select(x => (Guid?)x.UserId).FirstOrDefaultAsync();
        if (studentId == null) return NotFound(ApiResponse<string>.Fail("Thành viên không tồn tại"));
        return WriteResult(await _membership.RemoveAsync(classId, studentId.Value, Scope(teacherId)), false);
    }

    // Read-only: schedules assigned to a class this teacher owns.
    [HttpGet("{classId:int}/schedules")]
    public async Task<IActionResult> Schedules(int classId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var rows = await _db.ClassExamSchedules.AsNoTracking().Where(x => x.ClassId == classId)
            .OrderBy(x => x.ExamSchedule.StartTime).ThenBy(x => x.Id)
            .Select(x => new
            {
                id = x.ExamScheduleId, title = x.ExamSchedule.Title, examName = x.ExamSchedule.Exam.Name,
                subjectName = x.ExamSchedule.Subject != null ? x.ExamSchedule.Subject.Name : null,
                roomName = x.ExamSchedule.Room != null ? x.ExamSchedule.Room.Name : null,
                startTime = x.ExamSchedule.StartTime, endTime = x.ExamSchedule.EndTime,
                timeZoneStatus = x.ExamSchedule.IsTimeUtc ? "utc" : "unknown", status = x.ExamSchedule.Status
            }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows));
    }

    // Read-only results of approved members in schedules assigned to this teacher's class.
    // Scores only; question/answer review is never exposed here. Voided attempts show no score.
    [HttpGet("{classId:int}/results")]
    public async Task<IActionResult> Results(int classId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var rows = await (from a in _db.ExamAttempts.AsNoTracking()
                          join cs in _db.ClassExamSchedules.AsNoTracking() on a.ExamScheduleId equals cs.ExamScheduleId
                          join cu in _db.ClassUsers.AsNoTracking() on new { cs.ClassId, a.UserId } equals new { cu.ClassId, cu.UserId }
                          where cs.ClassId == classId && cu.Status == 1
                          orderby a.StartedAtUtc descending
                          select new
                          {
                              attemptId = a.Id, studentName = cu.User.FullName, studentEmail = cu.User.Email,
                              scheduleTitle = cs.ExamSchedule.Title, examName = cs.ExamSchedule.Exam.Name,
                              startedAt = a.StartedAtUtc, submittedAt = a.Submission != null ? (DateTime?)a.Submission.SubmittedAtUtc : null,
                              state = a.Submission != null && a.Submission.VoidedAtUtc != null ? "voided" : a.State,
                              totalMark = a.Submission != null && a.Submission.VoidedAtUtc == null ? (decimal?)a.Submission.TotalMark : null,
                              isPassed = a.Submission != null && a.Submission.VoidedAtUtc == null ? (bool?)a.Submission.IsPassed : null
                          }).Take(500).ToListAsync();
        var data = rows.Select(r => new
        {
            r.attemptId, r.studentName, r.studentEmail, r.scheduleTitle, r.examName,
            startedAt = DateTime.SpecifyKind(r.startedAt, DateTimeKind.Utc),
            submittedAt = r.submittedAt.HasValue ? (DateTime?)DateTime.SpecifyKind(r.submittedAt.Value, DateTimeKind.Utc) : null,
            r.state, r.totalMark, r.isPassed
        });
        return Ok(ApiResponse<object>.Ok(data));
    }

    // To-be T10 (Q8, Q13b): bảng xếp hạng đầy đủ của lớp — chỉ giảng viên của lớp và Admin. Chỉ điểm bài thi,
    // không câu hỏi/đáp án. Top/Bottom chỉ khi lớp có ≥ 5 học viên có điểm.
    [HttpGet("{classId:int}/ranking")]
    public async Task<IActionResult> Ranking(int classId, CancellationToken ct)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        var data = await _ranking.LoadAsync(classId, ct);
        if (data == null) return UnprocessableEntity(ApiResponse<string>.Fail("CLASS_TOO_LARGE"));
        var counted = data.Schedules.Where(x => x.Counted).ToList();
        var byUser = data.Result.Rows.ToDictionary(x => x.UserId);
        var students = data.Members.Select(m => (m, r: byUser[m.UserId]))
            .OrderBy(x => x.r.Rank ?? int.MaxValue).ThenBy(x => x.m.FullName).ThenBy(x => x.m.UserName)
            .Select(x => new
            {
                userId = x.m.UserId, userName = x.m.UserName, fullName = x.m.FullName, email = x.m.Email,
                rank = x.r.Rank, total = x.r.Total, attemptedCount = x.r.AttemptedCount, scheduleCount = counted.Count,
                passedCount = x.r.PassedCount, group = x.r.Group,
                scores = counted.Select(s => new { scheduleId = s.Id, score = x.r.Scores[s.Id] }).ToList()
            }).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            scope = "classExamSchedulesOnly", scale = 10, scoreMethod = "highestAttempt", absentScore = 0,
            minScoredForRanking = ClassRanking.MinScoredForRanking, scoredCount = data.Result.ScoredCount,
            rankingVisible = data.Result.RankingVisible, skippedAttempts = data.Result.SkippedAttempts,
            serverNow = data.ServerNow,
            schedules = counted.Select(s => new { id = s.Id, title = s.Title, examName = s.ExamName, endTime = s.EndTime }),
            notCounted = data.Schedules.Where(x => !x.Counted).Select(s => new
                { id = s.Id, title = s.Title, examName = s.ExamName, endTime = s.EndTime,
                  timeZoneStatus = s.IsTimeUtc ? "utc" : "unknown", reason = s.NotCountedReason }),
            students
        }));
    }

    // Admin (level 1) là quyền cao nhất: thao tác được trên mọi lớp, không bị giới hạn theo giảng viên phụ trách.
    private bool IsAdmin => User.HasClaim("level_id", "1");
    private Guid? Scope(Guid teacherId) => IsAdmin ? null : teacherId;

    private Task<bool> OwnsAsync(int classId, Guid teacherId) =>
        _db.Classes.AnyAsync(x => x.Id == classId && (IsAdmin || x.TeacherId == teacherId));

    private IActionResult WriteResult(MembershipResult result, bool created) => result.Outcome switch
    {
        MembershipOutcome.Created when created => Created("", ApiResponse<object>.Ok(MemberDto(result))),
        MembershipOutcome.Updated or MembershipOutcome.Removed => Ok(ApiResponse<object>.Ok(MemberDto(result))),
        MembershipOutcome.ForbiddenOwner => NotFound(ApiResponse<string>.Fail("Lớp không tồn tại")),
        MembershipOutcome.MissingClass or MembershipOutcome.MissingMembership => NotFound(ApiResponse<string>.Fail("Lớp hoặc thành viên không tồn tại")),
        MembershipOutcome.MissingStudent => BadRequest(ApiResponse<string>.Fail("Tài khoản học viên không hợp lệ")),
        MembershipOutcome.InactiveClass => Conflict(ApiResponse<string>.Fail("Lớp không hoạt động")),
        MembershipOutcome.Duplicate => Conflict(ApiResponse<string>.Fail("Học viên đã có trong lớp")),
        MembershipOutcome.Full => Conflict(ApiResponse<string>.Fail("Lớp đã đủ sĩ số")),
        MembershipOutcome.ScheduleConflict => Conflict(ApiResponse<string>.Fail("Học viên đã có lịch thi giao nhau ở lớp khác")),
        _ => Conflict(ApiResponse<string>.Fail("Trạng thái thành viên không hợp lệ"))
    };

    private static object MemberDto(MembershipResult result) => new
    { result.Member!.Id, result.Member.ClassId, result.Member.UserId, result.Member.Status };
}

public sealed class TeacherAddStudentRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
}
