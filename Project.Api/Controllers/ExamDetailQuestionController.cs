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
        public IActionResult Create([FromBody] ExamDetailQuestionCreateDto dto) =>
            StatusCode(405, ApiResponse<string>.Fail("Dùng /web/exams/{id}/variants để ghi tập câu hỏi."));

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] ExamDetailQuestionCreateDto dto) =>
            StatusCode(405, ApiResponse<string>.Fail("Dùng /web/exams/{id}/variants để ghi tập câu hỏi."));

        [HttpDelete("{id}")]
        public IActionResult Delete(int id) =>
            StatusCode(405, ApiResponse<string>.Fail("Dùng /web/exams/{id}/variants để ghi tập câu hỏi."));
    }
}
