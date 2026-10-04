using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.UserPermissionDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/user-permissions")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class UserPermissionController : ControllerBase
    {
        private readonly IUserPermissionService _service;
        private readonly ProjectDACNDbContext _db;

        public UserPermissionController(IUserPermissionService service, ProjectDACNDbContext db)
        {
            _service = service;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<UserPermissionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("User permission không tồn tại"));

            return Ok(ApiResponse<UserPermissionResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserPermissionCreateDto dto)
        {
            if (!await _db.Users.AnyAsync(x => x.Id == dto.UserId) ||
                !await _db.Permissions.AnyAsync(x => x.Id == dto.PermissionId))
                return BadRequest(ApiResponse<string>.Fail("Tài khoản hoặc quyền không tồn tại"));
            if (await _db.UserPermissions.AnyAsync(x => x.UserId == dto.UserId && x.PermissionId == dto.PermissionId))
                return Conflict(ApiResponse<string>.Fail("Tài khoản đã có quyền này"));
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo User permission thất bại"));

            return Created("", ApiResponse<UserPermissionResponseDto>.Ok(result, "Tạo User permission thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserPermissionCreateDto dto)
        {
            if (!await _db.Users.AnyAsync(x => x.Id == dto.UserId) ||
                !await _db.Permissions.AnyAsync(x => x.Id == dto.PermissionId))
                return BadRequest(ApiResponse<string>.Fail("Tài khoản hoặc quyền không tồn tại"));
            if (await _db.UserPermissions.AnyAsync(x => x.Id != id && x.UserId == dto.UserId && x.PermissionId == dto.PermissionId))
                return Conflict(ApiResponse<string>.Fail("Tài khoản đã có quyền này"));
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("User permission không tồn tại"));

            return Ok(ApiResponse<UserPermissionResponseDto>.Ok(result, "Cập nhật User permission thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("User permission không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa User permission với ID {id} thành công"));
        }
    }
}
