using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs;
using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.RoomDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/rooms")]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _service;

        public RoomController(IRoomService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms([FromQuery] RoomQueryDto query)
        {
            var result = await _service.GetRoomsAsync(query);
            return Ok(ApiResponse<PagedResult<RoomResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoomById(int id)
        {
            var result = await _service.GetRoomByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Room không tồn tại"));

            return Ok(ApiResponse<RoomResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoomAsync([FromBody] RoomCreateDto dto)
        {
            var result = await _service.CreateRoomAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Room thất bại"));

            return Created("", ApiResponse<RoomResponseDto>.Ok(result, "Add room successfully"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRoomAsync(int id, [FromBody] RoomCreateDto dto)
        {
            var result = await _service.UpdateRoomAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Room không tồn tại"));

            return Ok(ApiResponse<RoomResponseDto>.Ok(result, "Cập nhật Room thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRoomAsync(int id)
        {
            var isDeleted = await _service.DeleteRoomAsync(id);
            if (!isDeleted)
                return NotFound(ApiResponse<string>.Fail("Room không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Room với ID {id} thành công"));
        }
    }

}
