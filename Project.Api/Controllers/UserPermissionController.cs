using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.UserPermissionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/user-permissions")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class UserPermissionController : ControllerBase
    {
        private readonly IUserPermissionService _service;

        public UserPermissionController(IUserPermissionService service)
        {
            _service = service;
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
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo User permission thất bại"));

            return Created("", ApiResponse<UserPermissionResponseDto>.Ok(result, "Tạo User permission thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserPermissionCreateDto dto)
        {
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
