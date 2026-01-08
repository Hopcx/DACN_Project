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
            var isDeleted = await _userService.DeleteUsserAsync(id);

            if (!isDeleted)
            {
                return NotFound(ApiResponse<string>.Fail("User không tồn tại hoặc xóa thất bại."));
            }

            return Ok(ApiResponse<string>.Ok(null, $"Xóa User với ID {id} thành công."));

        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateRoomAsync(UserCreateDto dto)
        {
            var result = await _userService.CreateRoomAsync(dto);
            return Created("", ApiResponse<UserResponseDto>.Ok(result));
        }
    }
}
