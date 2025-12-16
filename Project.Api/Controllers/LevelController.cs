using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.LevelDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("api/levels")]
    public class LevelController : ControllerBase
    {
        private readonly ILevelService _service;

        public LevelController(ILevelService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<LevelResponseDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create(LevelCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return Created("", ApiResponse<LevelResponseDto>.Ok(result));
        }
    }

}
