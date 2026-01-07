using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
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
        public async Task<IActionResult> GetAllRoomAsync()
        {
            var result = await _service.GetAllRoomAsync();
            return Ok(ApiResponse<List<RoomResponseDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoomAsync(RoomCreateDto dto)
        {
            var result = await _service.CreateRoomAsync(dto);
            return Created("", ApiResponse<RoomResponseDto>.Ok(result));
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRoomAsync(int id)
        {
            var isDeleted = await _service.DeleteRoomAsync(id);

            if (!isDeleted)
            {
                return NotFound(ApiResponse<string>.Fail("Room không tồn tại hoặc xóa thất bại."));
            }

            return Ok(ApiResponse<string>.Ok($"Xóa Room với ID {id} thành công."));
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRoomAsync(int id, RoomCreateDto dto)
        {
            var result = await _service.UpdateRoomAsync(id, dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Cập nhật Level thất bại."));

            return Ok(ApiResponse<RoomResponseDto>.Ok(result, "Cập nhật Level thành công."));

        }
    }
}
