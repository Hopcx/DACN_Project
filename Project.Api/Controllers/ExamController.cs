using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamDTO;
using Project.Application.Interfaces.Services;
using Project.Application.DTOs.ExamDetailDTO;
using System.Security.Claims;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/exams")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ExamManagement")]
    public class ExamController : ControllerBase
    {
        private readonly IExamService _service;
        private readonly IExamVariantService _variants;

        public ExamController(IExamService service, IExamVariantService variants)
        {
            _service = service;
            _variants = variants;
        }

        [HttpGet("{id:int}/variants")]
        public async Task<IActionResult> GetVariants(int id) =>
            Ok(ApiResponse<List<ExamVariantResponseDto>>.Ok(await _variants.GetByExamAsync(id)));

        [HttpGet("{id:int}/question-options")]
        public async Task<IActionResult> GetQuestionOptions(int id)
        {
            try { return Ok(ApiResponse<List<ExamQuestionOptionDto>>.Ok(await _variants.GetQuestionOptionsAsync(id))); }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
        }

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects() =>
            Ok(ApiResponse<List<ExamSubjectOptionDto>>.Ok(await _variants.GetSubjectsAsync()));

        [HttpGet("{id:int}/variants/{variantId:int}")]
        public async Task<IActionResult> GetVariant(int id, int variantId)
        {
            var item = await _variants.GetAsync(id, variantId);
            return item == null ? NotFound(ApiResponse<string>.Fail("Mã đề không tồn tại."))
                : Ok(ApiResponse<ExamVariantResponseDto>.Ok(item));
        }

        [HttpPost("{id:int}/variants")]
        public Task<IActionResult> CreateVariant(int id, [FromBody] ExamVariantSaveDto dto) => SaveVariant(id, null, dto);

        [HttpPut("{id:int}/variants/{variantId:int}")]
        public Task<IActionResult> UpdateVariant(int id, int variantId, [FromBody] ExamVariantSaveDto dto) => SaveVariant(id, variantId, dto);

        private async Task<IActionResult> SaveVariant(int id, int? variantId, ExamVariantSaveDto dto)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
            try
            {
                var result = await _variants.SaveAsync(id, variantId, dto, actorId);
                return variantId.HasValue ? Ok(ApiResponse<ExamVariantResponseDto>.Ok(result))
                    : Created($"/web/exams/{id}/variants/{result.Id}", ApiResponse<ExamVariantResponseDto>.Ok(result));
            }
            catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? textSearch, [FromQuery] bool? isActive)
        {
            var result = await _service.GetAllAsync(textSearch, isActive);
            return Ok(ApiResponse<List<ExamResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam không tồn tại"));

            return Ok(ApiResponse<ExamResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExamCreateDto dto)
        {
            ExamResponseDto? result;
            try { result = await _service.CreateAsync(dto); }
            catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Exam thất bại"));

            return Created("", ApiResponse<ExamResponseDto>.Ok(result, "Tạo Exam thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ExamCreateDto dto)
        {
            ExamResponseDto? result;
            try { result = await _service.UpdateAsync(id, dto); }
            catch (ArgumentException ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam không tồn tại"));

            return Ok(ApiResponse<ExamResponseDto>.Ok(result, "Cập nhật Exam thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted;
            try { deleted = await _service.DeleteAsync(id); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse<string>.Fail(ex.Message)); }
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Exam không tồn tại hoặc xóa thất bại"));

            return Ok(ApiResponse<string>.Ok($"Xóa Exam với ID {id} thành công"));
        }
    }
}
