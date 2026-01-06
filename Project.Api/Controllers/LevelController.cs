using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.LevelDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/levels")]
    public class LevelController : ControllerBase
    {
        private readonly ILevelService _service;

        public LevelController(ILevelService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllLevelAsync()
        {
            var result = await _service.GetAllLevelAsync();
            return Ok(ApiResponse<List<LevelResponseDto>>.Ok(result));
        }


        [HttpPost]
        public async Task<IActionResult> CreateLevelAsync(LevelCreateDto dto)
        {
            var result = await _service.CreateLevelAsync(dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Level thất bại."));

            return Created("", ApiResponse<LevelResponseDto>.Ok(result));
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLevelAsync(int id, LevelUpdateDto dto)
        {
            // Kiểm tra ID từ URL và DTO có khớp không
            if (id != dto.Id)
            {
                return BadRequest(ApiResponse<string>.Fail("ID không khớp."));
            }

            var result = await _service.UpdateLevelAsync(dto);

            if (result == null)
            {
                return NotFound(ApiResponse<string>.Fail("Level không tồn tại hoặc cập nhật thất bại."));
            }

            return Ok(ApiResponse<LevelResponseDto>.Ok(result));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLevelAsync(int id)
        {
            var isDeleted = await _service.DeleteLevelAsync(id);

            if (!isDeleted)
            {
                return NotFound(ApiResponse<string>.Fail("Level không tồn tại hoặc xóa thất bại."));
            }

            return Ok(ApiResponse<string>.Ok($"Xóa Level với ID {id} thành công."));
        }

        [HttpGet("{levelId}/users")]
        public async Task<IActionResult> GetUsersByLevelAsync(int levelId, [FromQuery] string? search)
        {
            var users = await _service.GetUsersByLevelAsync(levelId, search);

            if (users == null || !users.Any())
            {
                return NotFound(ApiResponse<string>.Fail("Không tìm thấy user nào cho Level này."));
            }

            return Ok(ApiResponse<List<UserLevelResponseDto>>.Ok(users));
        }
    }

}
