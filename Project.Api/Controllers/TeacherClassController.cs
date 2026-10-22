using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize(Policy = "Teacher")]
[Route("web/teacher/classes")]
public class TeacherClassController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly ClassMembershipStore _membership;

    public TeacherClassController(ProjectDACNDbContext db, ClassMembershipStore membership)
    { _db = db; _membership = membership; }

    private bool TryTeacher(out Guid teacherId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out teacherId);

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        var rows = await _db.Classes.AsNoTracking().Where(x => x.TeacherId == teacherId)
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
        return WriteResult(await _membership.JoinAsync(classId, studentId.Value, true, teacherId: teacherId), true);
    }

    [HttpPost("{classId:int}/members/{membershipId:int}/approve")]
    public async Task<IActionResult> Approve(int classId, int membershipId)
    {
        if (!TryTeacher(out var teacherId)) return Unauthorized();
        if (!await OwnsAsync(classId, teacherId)) return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
        if (!await _db.ClassUsers.AnyAsync(x => x.Id == membershipId && x.ClassId == classId))
            return NotFound(ApiResponse<string>.Fail("Thành viên không tồn tại"));
        return WriteResult(await _membership.ApproveAsync(membershipId, teacherId), false);
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
        return WriteResult(await _membership.RemoveAsync(classId, studentId.Value, teacherId), false);
    }

    private Task<bool> OwnsAsync(int classId, Guid teacherId) =>
        _db.Classes.AnyAsync(x => x.Id == classId && x.TeacherId == teacherId);

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
