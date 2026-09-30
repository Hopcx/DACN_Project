using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.RoomDTO;
using Project.Application.DTOs.UserDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/users")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllUserAsync()
        {
            var result = await _userService.GetAllUsserAsync();
            return Ok(ApiResponse<List<UserResponseDto>>.Ok(result));
        }
        [HttpDelete("delete-user-{id}")]
        public async Task<IActionResult> DeleteUsserAsync(string id)
        {
            // ===== CÁCH CŨ: Manual check và return (đã comment) =====
            //var isDeleted = await _userService.DeleteUsserAsync(id);
            //if (!isDeleted)
            //{
            //    return NotFound(ApiResponse<string>.Fail("User không tồn tại hoặc xóa thất bại."));
            //}
            //return Ok(ApiResponse<string>.Ok(null, $"Xóa User với ID {id} thành công."));

            // ===== CÁCH MỚI: Service throw exception, middleware xử lý =====
            await _userService.DeleteUsserAsync(id);
            return Ok(ApiResponse<string>.Ok(null, $"Xóa User với ID {id} thành công."));
        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUserAsync(UserCreateDto dto)
        {
            // ===== CÁCH CŨ: Manual check và return (đã comment) =====
            //var result = await _userService.CreateUserAsync(dto);
            //if (result == null)
            //    return BadRequest(ApiResponse<string>.Fail("Tạo User thất bại"));
            //return Created("", ApiResponse<UserResponseDto>.Ok(result));

            // ===== CÁCH MỚI: FluentValidation tự động validate, service throw exception nếu fail =====
            // FluentValidation sẽ tự động validate dto trước khi vào method này
            var result = await _userService.CreateUserAsync(dto);
            return Created("", ApiResponse<UserResponseDto>.Ok(result, "Create user succesfully"));
        }
    }
}
