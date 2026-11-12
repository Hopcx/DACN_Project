using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.LogDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Authorize(Policy = "AdminManagement")]
    [Route("web/logs")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class LogController : ControllerBase
    {
        private readonly ILogService _service;

        public LogController(ILogService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<LogResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Log không tồn tại"));

            return Ok(ApiResponse<LogResponseDto>.Ok(result));
        }

        [HttpPost]
        public IActionResult Create([FromBody] LogCreateDto dto)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Nhật ký chỉ được ghi bởi hệ thống."));
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] LogCreateDto dto)
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
