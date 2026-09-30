using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ClassExamScheduleDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/class-exam-schedules")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ScheduleManagement")]
    public class ClassExamScheduleController : ControllerBase
    {
        private readonly IClassExamScheduleService _service;

        public ClassExamScheduleController(IClassExamScheduleService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ClassExamScheduleResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại"));

            return Ok(ApiResponse<ClassExamScheduleResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassExamScheduleCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Class exam schedule thất bại"));

            return Created("", ApiResponse<ClassExamScheduleResponseDto>.Ok(result, "Tạo Class exam schedule thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassExamScheduleCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại"));

            return Ok(ApiResponse<ClassExamScheduleResponseDto>.Ok(result, "Cập nhật Class exam schedule thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Class exam schedule với ID {id} thành công"));
        }
    }
}
