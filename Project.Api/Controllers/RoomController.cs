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
    }
}
