using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamActivityLogDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Authorize(Policy = "AdminManagement")]
    [Route("web/exam-activity-logs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class ExamActivityLogController : ControllerBase
    {
        private readonly IExamActivityLogService _service;

        public ExamActivityLogController(IExamActivityLogService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ExamActivityLogResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam activity log không tồn tại"));

            return Ok(ApiResponse<ExamActivityLogResponseDto>.Ok(result));
        }

        [HttpPost]
        public IActionResult Create([FromBody] ExamActivityLogCreateDto dto)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Nhật ký chỉ được ghi bởi hệ thống."));
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] ExamActivityLogCreateDto dto)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Nhật ký chỉ được ghi bởi hệ thống."));
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Nhật ký chỉ được ghi bởi hệ thống."));
        }
    }
}
