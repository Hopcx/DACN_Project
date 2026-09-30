using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.SubmissionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/submissions")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class SubmissionController : ControllerBase
    {
        private readonly ISubmissionService _service;

        public SubmissionController(ISubmissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<SubmissionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Submission không tồn tại"));

            return Ok(ApiResponse<SubmissionResponseDto>.Ok(result));
        }

        // Write route is closed until server-owned finalization or audited correction exists.
        [NonAction]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SubmissionCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Submission thất bại"));

            return Created("", ApiResponse<SubmissionResponseDto>.Ok(result, "Tạo Submission thành công"));
        }

        // Write route is closed until server-owned finalization or audited correction exists.
        [NonAction]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SubmissionCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Submission không tồn tại"));

            return Ok(ApiResponse<SubmissionResponseDto>.Ok(result, "Cập nhật Submission thành công"));
        }

        // Write route is closed until server-owned finalization or audited correction exists.
        [NonAction]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Submission không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Submission với ID {id} thành công"));
        }
    }
}
