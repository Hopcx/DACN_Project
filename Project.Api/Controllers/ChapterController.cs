using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.Services;
using Project.Application.Common;

namespace Project.Api.Controllers;

// To-be T2 (QT2-2 a): xem = quyền câu hỏi HOẶC môn học; thêm/sửa/ẩn = quyền môn học (Admin luôn được).
[ApiController]
[Route("web/chapters")]
[Authorize(Policy = "QuestionOrSubjectManagement")]
public class ChapterController : ControllerBase
{
    private readonly ChapterStore _store;
    private readonly BestEffortAudit _audit;
    public ChapterController(ChapterStore store, BestEffortAudit audit) { _store = store; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? subjectId, [FromQuery] bool includeHidden = false) =>
        Ok(ApiResponse<List<ChapterResponse>>.Ok(await _store.ListAsync(subjectId, includeHidden)));

    [HttpPost]
    [Authorize(Policy = "SubjectManagement")]
    public async Task<IActionResult> Create([FromBody] ChapterRequest request)
    {
        var result = await _store.CreateAsync(request, Actor());
        await _audit.WriteAsync(User, "chapter.create", "Chapter", result.Id.ToString(), $"Tạo chương #{result.Id} của môn {result.SubjectId}");
        return Created("", ApiResponse<ChapterResponse>.Ok(result, "Đã tạo chương"));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "SubjectManagement")]
    public async Task<IActionResult> Update(int id, [FromBody] ChapterRequest request)
    {
        var result = await _store.UpdateAsync(id, request, Actor());
        if (result == null) return NotFound(ApiResponse<string>.Fail("Chương không tồn tại hoặc đã bị ẩn"));
        await _audit.WriteAsync(User, "chapter.update", "Chapter", id.ToString(), $"Sửa chương #{id} của môn {result.SubjectId}; trạng thái {result.Status}");
        return Ok(ApiResponse<ChapterResponse>.Ok(result, "Đã cập nhật chương"));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "SubjectManagement")]
    public async Task<IActionResult> Hide(int id)
    {
        if (!await _store.HideAsync(id, Actor())) return NotFound(ApiResponse<string>.Fail("Chương không tồn tại hoặc đã bị ẩn"));
        await _audit.WriteAsync(User, "chapter.hide", "Chapter", id.ToString(), $"Ẩn chương #{id}");
        return Ok(ApiResponse<string>.Ok("Đã ẩn chương"));
    }

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
