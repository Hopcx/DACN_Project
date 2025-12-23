using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.LevelDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/levels")]
    public class LevelController : ControllerBase
    {
        private readonly ILevelService _service;

        public LevelController(ILevelService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllLevelAsync()
        {
            var result = await _service.GetAllLevelAsync();
            return Ok(ApiResponse<List<LevelResponseDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateLevelAsync(LevelCreateDto dto)
        {
            var result = await _service.CreateLevelAsync(dto);
            return Created("", ApiResponse<LevelResponseDto>.Ok(result));
        }
    }

}
