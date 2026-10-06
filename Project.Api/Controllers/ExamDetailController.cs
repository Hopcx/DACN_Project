using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamDetailDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/exam-details")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ExamManagement")]
    public class ExamDetailController : ControllerBase
    {
        private readonly IExamDetailService _service;

        public ExamDetailController(IExamDetailService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ExamDetailResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam detail không tồn tại"));

            return Ok(ApiResponse<ExamDetailResponseDto>.Ok(result));
        }

        [HttpPost]
        public IActionResult Create([FromBody] ExamDetailCreateDto dto) =>
            StatusCode(405, ApiResponse<string>.Fail("Dùng /web/exams/{id}/variants để ghi mã đề."));

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] ExamDetailCreateDto dto) =>
            StatusCode(405, ApiResponse<string>.Fail("Dùng /web/exams/{id}/variants để ghi mã đề."));

        [HttpDelete("{id}")]
        public IActionResult Delete(int id) =>
            StatusCode(405, ApiResponse<string>.Fail("Không hỗ trợ xóa mã đề qua CRUD rời."));
    }
}
