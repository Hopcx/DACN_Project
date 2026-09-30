using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.QuestionLevelDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/question-levels")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "QuestionManagement")]
    public class QuestionLevelController : ControllerBase
    {
        private readonly IQuestionLevelService _service;

        public QuestionLevelController(IQuestionLevelService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllQuestionLevels([FromQuery] string? textSearch)
        {
            var result = await _service.GetAllQuestionLevelsAsync(textSearch);
            return Ok(ApiResponse<List<QuestionLevelResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuestionLevelById(int id)
        {
            var result = await _service.GetQuestionLevelByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question level không tồn tại"));

            return Ok(ApiResponse<QuestionLevelResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuestionLevel([FromBody] QuestionLevelCreateDto dto)
        {
            var result = await _service.CreateQuestionLevelAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Question level thất bại"));

            return Created("", ApiResponse<QuestionLevelResponseDto>.Ok(result, "Tạo Question level thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestionLevel(int id, [FromBody] QuestionLevelCreateDto dto)
        {
            var result = await _service.UpdateQuestionLevelAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question level không tồn tại"));

            return Ok(ApiResponse<QuestionLevelResponseDto>.Ok(result, "Cập nhật Question level thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestionLevel(int id)
        {
            var deleted = await _service.DeleteQuestionLevelAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Question level không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Question level với ID {id} thành công"));
        }
    }
}
