using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.PermissionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/permissions")]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _service;
        public PermissionController(IPermissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPermissionAsync()
        {
            var result = await _service.GetAllPermissionAsync();
            return Ok(ApiResponse<List<PermissionResponseDto>>.Ok(result));
        }


        [HttpPost]
        public async Task<IActionResult> CreatePermissionAsync(PermissionCreateDto dto)
        {
            var result = await _service.CreatePermissionsAsync(dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Permission thất bại."));

            return Created("", ApiResponse<PermissionResponseDto>.Ok(result));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePermissionAsync(int id)
        {
            var isDeleted = await _service.DeletePermissionAsync(id);

            if (!isDeleted)
            {
                return NotFound(ApiResponse<string>.Fail("Permission không tồn tại hoặc xóa thất bại."));
            }

            return Ok(ApiResponse<string>.Ok($"Xóa Permission với ID {id} thành công."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePermissionAsync(int id, PermissionCreateDto dto)
        {
            var result = await _service.UpdatePermissionsAsync(id, dto);

            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Cập nhật Permission thất bại."));

            return Ok(ApiResponse<PermissionResponseDto>.Ok(result, "Cập nhật Permission thành công."));
        }

    }
}
