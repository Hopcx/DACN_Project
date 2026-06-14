using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/questions")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _service;

        public QuestionController(IQuestionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? textSearch)
        {
            var result = await _service.GetAllAsync(textSearch);
            return Ok(ApiResponse<List<QuestionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question không tồn tại"));

            return Ok(ApiResponse<QuestionResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] QuestionCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Question thất bại"));

            return Created("", ApiResponse<QuestionResponseDto>.Ok(result, "Tạo Question thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] QuestionCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question không tồn tại"));

            return Ok(ApiResponse<QuestionResponseDto>.Ok(result, "Cập nhật Question thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Question không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Question với ID {id} thành công"));
        }
    }
}
