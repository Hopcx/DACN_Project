using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.SubjectDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/subjects")]
    public class SubjectController : ControllerBase
    {
        private readonly ISubjectService _service;

        public SubjectController(ISubjectService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSubjects([FromQuery] string? textSearch, [FromQuery] bool? isActive)
        {
            var result = await _service.GetAllSubjectsAsync(textSearch, isActive);
            return Ok(ApiResponse<List<SubjectResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubjectById(int id)
        {
            var result = await _service.GetSubjectByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Subject không tồn tại"));

            return Ok(ApiResponse<SubjectResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] SubjectCreateDto dto)
        {
            var result = await _service.CreateSubjectAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Subject thất bại"));

            return Created("", ApiResponse<SubjectResponseDto>.Ok(result, "Tạo Subject thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] SubjectCreateDto dto)
        {
            var result = await _service.UpdateSubjectAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Subject không tồn tại"));

            return Ok(ApiResponse<SubjectResponseDto>.Ok(result, "Cập nhật Subject thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var deleted = await _service.DeleteSubjectAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Subject không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Subject với ID {id} thành công"));
        }
    }
}
