using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.QuestionTypeDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/question-types")]
    public class QuestionTypeController : ControllerBase
    {
        private readonly IQuestionTypeService _service;

        public QuestionTypeController(IQuestionTypeService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllQuestionTypes()
        {
            var result = await _service.GetAllQuestionTypesAsync();
            return Ok(ApiResponse<List<QuestionTypeResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuestionTypeById(int id)
        {
            var result = await _service.GetQuestionTypeByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question type không tồn tại"));

            return Ok(ApiResponse<QuestionTypeResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuestionType([FromBody] QuestionTypeCreateDto dto)
        {
            var result = await _service.CreateQuestionTypeAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Question type thất bại"));

            return Created("", ApiResponse<QuestionTypeResponseDto>.Ok(result, "Tạo Question type thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestionType(int id, [FromBody] QuestionTypeCreateDto dto)
        {
            var result = await _service.UpdateQuestionTypeAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question type không tồn tại"));

            return Ok(ApiResponse<QuestionTypeResponseDto>.Ok(result, "Cập nhật Question type thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestionType(int id)
        {
            var deleted = await _service.DeleteQuestionTypeAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Question type không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Question type với ID {id} thành công"));
        }
    }
}
