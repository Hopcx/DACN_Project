using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs;
using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.RoomDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/rooms")]
    // Ví dụ gắn Policy cho controller:
    // - Chỉ user có PermissionId = 4 ("Quản lý lịch thi") mới được thao tác phòng thi
    [Authorize(Policy = "ScheduleManagement")]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _service;
        private readonly ProjectDACNDbContext _db;

        public RoomController(IRoomService service, ProjectDACNDbContext db)
        {
            _service = service;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms([FromQuery] RoomQueryDto query)
        {
            if (query.MinCapacity > query.MaxCapacity)
                return BadRequest(ApiResponse<string>.Fail("Sức chứa tối thiểu phải nhỏ hơn hoặc bằng sức chứa tối đa"));
            if ((long)(query.Page - 1) * query.PageSize > int.MaxValue)
                return BadRequest(ApiResponse<string>.Fail("Trang yêu cầu vượt giới hạn"));
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
            if (await _db.ExamSchedules.AnyAsync(x => x.RoomId == id))
                return Conflict(ApiResponse<string>.Fail("Không thể xóa phòng đã có lịch thi"));
            bool isDeleted;
            try { isDeleted = await _service.DeleteRoomAsync(id); }
            catch (DbUpdateException)
            {
                return Conflict(ApiResponse<string>.Fail("Không thể xóa phòng đang được sử dụng"));
            }
            if (!isDeleted)
                return NotFound(ApiResponse<string>.Fail("Room không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Room với ID {id} thành công"));
        }
    }

}
