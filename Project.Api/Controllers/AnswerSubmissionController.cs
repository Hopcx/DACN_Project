using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.AnswerSubmissionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/answer-submissions")]
    public class AnswerSubmissionController : ControllerBase
    {
        private readonly IAnswerSubmissionService _service;

        public AnswerSubmissionController(IAnswerSubmissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<AnswerSubmissionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Answer submission không tồn tại"));

            return Ok(ApiResponse<AnswerSubmissionResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AnswerSubmissionCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Answer submission thất bại"));

            return Created("", ApiResponse<AnswerSubmissionResponseDto>.Ok(result, "Tạo Answer submission thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AnswerSubmissionCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Answer submission không tồn tại"));

            return Ok(ApiResponse<AnswerSubmissionResponseDto>.Ok(result, "Cập nhật Answer submission thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Answer submission không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Answer submission với ID {id} thành công"));
        }
    }
}
