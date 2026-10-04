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
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "QuestionManagement")]
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
        public IActionResult CreateAnswerAsync(AnswerCreateDto dto)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Hãy lưu câu hỏi cùng toàn bộ đáp án qua /web/questions."));
        }




        [HttpDelete("{id}")]
        public IActionResult DeleteAnswerAsync(int id)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Hãy lưu câu hỏi cùng toàn bộ đáp án qua /web/questions."));
        }

        [HttpPut("{id}")]
        public IActionResult UpdateAnswerAsync(int id, AnswerCreateDto dto)
        {
            return StatusCode(405, ApiResponse<string>.Fail("Hãy lưu câu hỏi cùng toàn bộ đáp án qua /web/questions."));

        }

    }
}
