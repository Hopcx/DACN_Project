using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Interfaces.Services;

namespace Project.Api.Controllers
{
    [ApiController]
    [Authorize(Policy = "QuestionManagement")]
    [Route("web/questions")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "QuestionManagement")]
    [Project.Api.Services.DeadlockAsConflict]   // T19: lưu câu dùng Serializable; deadlock (chủ yếu hai PUT cùng một câu) → 409
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _service;
        private readonly Project.Api.Services.BestEffortAudit _audit;

        public QuestionController(IQuestionService service, Project.Api.Services.BestEffortAudit audit)
        {
            _service = service;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? textSearch, [FromQuery] int? subjectId,
            [FromQuery] int? questionTypeId, [FromQuery] int? questionLevelId,
            [FromQuery] int? chapterId, [FromQuery] List<int>? tagIds, [FromQuery] int? page, [FromQuery] int? pageSize)
        {
            // T19 (nợ L7): có page → trả một trang { page, pageSize, totalCount, items }; không có → mảng như trước (tương thích ngược)
            if (page.HasValue || pageSize.HasValue)
                return Ok(ApiResponse<QuestionPageDto>.Ok(await _service.GetPageAsync(textSearch, subjectId, questionTypeId, questionLevelId,
                    chapterId, tagIds, page ?? 1, pageSize ?? 20)));
            var result = await _service.GetAllAsync(textSearch, subjectId, questionTypeId, questionLevelId, chapterId, tagIds);
            return Ok(ApiResponse<List<QuestionResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Question không tồn tại"));

            return Ok(ApiResponse<QuestionResponseDto>.Ok(result));
        }

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects()
        {
            var result = await _service.GetSubjectsAsync();
            return Ok(ApiResponse<List<QuestionSubjectDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] QuestionCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Question thất bại"));
            await _audit.WriteAsync(User, "question.create", "Question", result.Id.ToString(), Describe(result));

            return Created("", ApiResponse<QuestionResponseDto>.Ok(result, "Tạo Question thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] QuestionCreateDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return await _service.GetByIdAsync(id) == null
                    ? NotFound(ApiResponse<string>.Fail("Question không tồn tại"))
                    : Conflict(ApiResponse<string>.Fail("Câu hỏi đã được sử dụng, không thể sửa đáp án hoặc nội dung."));
            // Đang ẩn (255) mà lưu với trạng thái 1 là "Hiện" lại câu hỏi
            // T19 (nợ L2): trạng thái cũ lấy từ chính lần đọc trong transaction lưu
            var action = result.PreviousStatus == 255 && result.Status == 1 ? "question.restore" : "question.update";
            await _audit.WriteAsync(User, action, "Question", id.ToString(), Describe(result));

            return Ok(ApiResponse<QuestionResponseDto>.Ok(result, "Cập nhật Question thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return await _service.GetByIdAsync(id) == null
                    ? NotFound(ApiResponse<string>.Fail("Question không tồn tại"))
                    : Conflict(ApiResponse<string>.Fail("Câu hỏi đã được sử dụng, không thể ẩn."));
            await _audit.WriteAsync(User, "question.hide", "Question", id.ToString(), $"Ẩn câu hỏi #{id}");

            return Ok(ApiResponse<string>.Ok($"Xóa Question với ID {id} thành công"));
        }

        // Chỉ id/môn/chương/số tag — không ghi nội dung câu hỏi hay đáp án vào audit
        private static string Describe(QuestionResponseDto q) =>
            $"Câu hỏi #{q.Id}; môn {q.SubjectId}; chương {(q.ChapterId?.ToString() ?? "—")}; {q.TagIds.Count} tag; trạng thái {q.Status}";
    }
}
