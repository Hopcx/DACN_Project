using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ClassUserDTO;
using Project.Application.Interfaces.Services;
using Project.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/class-users")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class ClassUserController : ControllerBase
    {
        private readonly IClassUserService _service;
        private readonly ClassMembershipStore _membership;
        private readonly ProjectDACNDbContext _db;

        public ClassUserController(IClassUserService service, ClassMembershipStore membership, ProjectDACNDbContext db)
        {
            _service = service;
            _membership = membership;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ClassUserResponseDto>>.Ok(result));
        }

        [HttpGet("by-class/{classId:int}")]
        public async Task<IActionResult> GetByClass(int classId)
        {
            if (!await _db.Classes.AnyAsync(x => x.Id == classId))
                return NotFound(ApiResponse<string>.Fail("Lớp không tồn tại"));
            var members = await _db.ClassUsers.AsNoTracking().Where(x => x.ClassId == classId)
                .OrderBy(x => x.Id).Select(x => new ClassUserResponseDto
                { Id = x.Id, ClassId = x.ClassId, UserId = x.UserId, Status = x.Status }).ToListAsync();
            return Ok(ApiResponse<List<ClassUserResponseDto>>.Ok(members));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class user không tồn tại"));

            return Ok(ApiResponse<ClassUserResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassUserCreateDto dto)
        {
            if (dto.Status != 1) return BadRequest(ApiResponse<string>.Fail("Quản trị chỉ được thêm thành viên đã duyệt"));
            return MembershipResponse(await _membership.JoinAsync(dto.ClassId, dto.UserId, true), true);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassUserCreateDto dto)
        {
            var existing = await _db.ClassUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound(ApiResponse<string>.Fail("Thành viên không tồn tại"));
            if (existing.ClassId != dto.ClassId || existing.UserId != dto.UserId || dto.Status != 1)
                return BadRequest(ApiResponse<string>.Fail("Chỉ được duyệt yêu cầu của đúng thành viên"));
            return MembershipResponse(await _membership.ApproveAsync(id), false);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.ClassUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound(ApiResponse<string>.Fail("Thành viên không tồn tại"));
            return MembershipResponse(await _membership.RemoveAsync(existing.ClassId, existing.UserId), false);
        }

        private IActionResult MembershipResponse(MembershipResult result, bool created) => result.Outcome switch
        {
            MembershipOutcome.Created when created => Created("", ApiResponse<ClassUserResponseDto>.Ok(ToDto(result))),
            MembershipOutcome.Updated or MembershipOutcome.Removed => Ok(ApiResponse<ClassUserResponseDto>.Ok(ToDto(result))),
            MembershipOutcome.MissingClass or MembershipOutcome.MissingMembership => NotFound(ApiResponse<string>.Fail("Lớp hoặc thành viên không tồn tại")),
            MembershipOutcome.MissingStudent => BadRequest(ApiResponse<string>.Fail("Tài khoản học viên không hợp lệ")),
            MembershipOutcome.InactiveClass => Conflict(ApiResponse<string>.Fail("Lớp không hoạt động")),
            MembershipOutcome.Duplicate => Conflict(ApiResponse<string>.Fail("Học viên đã có trong lớp")),
            MembershipOutcome.Full => Conflict(ApiResponse<string>.Fail("Lớp đã đủ sĩ số")),
            MembershipOutcome.ScheduleConflict => Conflict(ApiResponse<string>.Fail("Học viên đã có lịch thi giao nhau ở lớp khác")),
            _ => Conflict(ApiResponse<string>.Fail("Trạng thái thành viên không hợp lệ"))
        };

        private static ClassUserResponseDto ToDto(MembershipResult result) => new()
        { Id = result.Member!.Id, ClassId = result.Member.ClassId, UserId = result.Member.UserId, Status = result.Member.Status };
    }
}
