using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamScheduleDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/exam-schedules")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ScheduleManagement")]
    public class ExamScheduleController : ControllerBase
    {
        private readonly IExamScheduleService _service;

        public ExamScheduleController(IExamScheduleService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExamSchedules()
        {
            var result = await _service.GetAllExamSchedulesAsync();
            return Ok(ApiResponse<List<ExamScheduleResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExamScheduleById(int id)
        {
            var result = await _service.GetExamScheduleByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));

            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateExamSchedule([FromBody] ExamScheduleCreateDto dto)
        {
            var result = await _service.CreateExamScheduleAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Exam schedule thất bại"));

            return Created("", ApiResponse<ExamScheduleResponseDto>.Ok(result, "Tạo Exam schedule thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExamSchedule(int id, [FromBody] ExamScheduleCreateDto dto)
        {
            var result = await _service.UpdateExamScheduleAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));

            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(result, "Cập nhật Exam schedule thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExamSchedule(int id)
        {
            var deleted = await _service.DeleteExamScheduleAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Exam schedule với ID {id} thành công"));
        }
    }
}
