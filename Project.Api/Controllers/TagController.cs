using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.Services;
using Project.Application.Common;

namespace Project.Api.Controllers;

// To-be T2 (QT2-3 a): xem/tạo = quyền câu hỏi; đổi tên/ẩn = Admin.
[ApiController]
[Route("web/tags")]
[Authorize(Policy = "QuestionManagement")]
public class TagController : ControllerBase
{
    private readonly TagStore _store;
    private readonly BestEffortAudit _audit;
    public TagController(TagStore store, BestEffortAudit audit) { _store = store; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? textSearch, [FromQuery] bool includeHidden = false) =>
        Ok(ApiResponse<List<TagResponse>>.Ok(await _store.ListAsync(textSearch, includeHidden)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TagRequest request)
    {
        var result = await _store.CreateAsync(request, Actor());
        await _audit.WriteAsync(User, "tag.create", "Tag", result.Id.ToString(), $"Tạo tag #{result.Id}");
        return Created("", ApiResponse<TagResponse>.Ok(result, "Đã tạo tag"));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminManagement")]
    public async Task<IActionResult> Update(int id, [FromBody] TagRequest request)
    {
        var result = await _store.UpdateAsync(id, request, Actor());
        if (result == null) return NotFound(ApiResponse<string>.Fail("Tag không tồn tại"));
        await _audit.WriteAsync(User, "tag.update", "Tag", id.ToString(), $"Sửa tag #{id}; trạng thái {result.Status}");
        return Ok(ApiResponse<TagResponse>.Ok(result, "Đã cập nhật tag"));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminManagement")]
    public async Task<IActionResult> Hide(int id)
    {
        if (!await _store.HideAsync(id, Actor())) return NotFound(ApiResponse<string>.Fail("Tag không tồn tại hoặc đã bị ẩn"));
        await _audit.WriteAsync(User, "tag.hide", "Tag", id.ToString(), $"Ẩn tag #{id}");
        return Ok(ApiResponse<string>.Ok("Đã ẩn tag"));
    }

    private Guid? Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
