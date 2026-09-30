using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamDetailQuestionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/exam-detail-questions")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ExamManagement")]
    public class ExamDetailQuestionController : ControllerBase
    {
        private readonly IExamDetailQuestionService _service;

        public ExamDetailQuestionController(IExamDetailQuestionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ExamDetailQuestionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam detail question không tồn tại"));

            return Ok(ApiResponse<ExamDetailQuestionResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExamDetailQuestionCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Exam detail question thất bại"));

            return Created("", ApiResponse<ExamDetailQuestionResponseDto>.Ok(result, "Tạo Exam detail question thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ExamDetailQuestionCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam detail question không tồn tại"));

            return Ok(ApiResponse<ExamDetailQuestionResponseDto>.Ok(result, "Cập nhật Exam detail question thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Exam detail question không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Exam detail question với ID {id} thành công"));
        }
    }
}
