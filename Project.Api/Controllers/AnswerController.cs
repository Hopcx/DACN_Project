using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.AnswerCreateDto;
using Project.Application.DTOs.LevelDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    
    [ApiController]
    [Route("web/answers")]
    public class AnswerController : ControllerBase
    {
        private readonly IAnswerService _service;
        public AnswerController(IAnswerService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAnswersAsync()
        {
            var result = await _service.GetAllAnswersAsync();
            return Ok(ApiResponse<List<AnswerResponseDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateAnswerAsync(AnswerCreateDto dto)
        {
            var result = await _service.CreateAnswerAsync(dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Answer thất bại."));

            return Created("", ApiResponse<AnswerResponseDto>.Ok(result));
        }




        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnswerAsync(int id)
        {
            var isDeleted = await _service.DeleteAnswerAsync(id);

            if (!isDeleted)
            {
                return NotFound(ApiResponse<string>.Fail("Answer không tồn tại hoặc xóa thất bại."));
            }

            return Ok(ApiResponse<string>.Ok($"Xóa Answer với ID {id} thành công."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAnswerAsync(int id, AnswerCreateDto dto)
        {
            var result = await _service.UpdateAnswerAsync(id, dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Cập nhật Answer thất bại."));

            return Ok(ApiResponse<AnswerResponseDto>.Ok(result, "Cập nhật Answer thành công."));

        }

    }
}
