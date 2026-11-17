using Microsoft.EntityFrameworkCore;
using Project.Application.Exceptions;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

public sealed record ChapterRequest(int SubjectId, string? Name, int SortOrder, byte? Status);
public sealed record ChapterResponse(int Id, int SubjectId, string Name, int SortOrder, byte Status, int QuestionCount);

// To-be T2: chương của môn (Q1, QT2-2). Status 1 hoạt động, 0 tạm ngừng, 255 đã ẩn; xóa = ẩn.
public sealed class ChapterStore
{
    private readonly ProjectDACNDbContext _db;
    public ChapterStore(ProjectDACNDbContext db) => _db = db;

    public async Task<List<ChapterResponse>> ListAsync(int? subjectId, bool includeHidden)
    {
        if (subjectId is <= 0) throw new BadRequestException("Môn phải là ID dương.");
        var query = _db.Chapters.AsNoTracking();
        if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId.Value);
        if (!includeHidden) query = query.Where(x => x.Status != 255);
        return await query.OrderBy(x => x.SubjectId).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ChapterResponse(x.Id, x.SubjectId, x.Name, x.SortOrder, x.Status, x.Questions.Count))
            .ToListAsync();
    }

    public async Task<ChapterResponse> CreateAsync(ChapterRequest request, Guid? actor)
    {
        var name = Validate(request);
        await EnsureSubjectAsync(request.SubjectId);
        await EnsureUniqueAsync(request.SubjectId, name, null);
        var chapter = new Chapter
        {
            SubjectId = request.SubjectId, Name = name, SortOrder = request.SortOrder,
            Status = request.Status ?? 1, CreatedBy = actor, CreatedAt = DateTime.UtcNow
        };
        _db.Chapters.Add(chapter);
        await SaveAsync();
        return Map(chapter, 0);
    }

    public async Task<ChapterResponse?> UpdateAsync(int id, ChapterRequest request, Guid? actor)
    {
        var name = Validate(request);
        var chapter = await _db.Chapters.FirstOrDefaultAsync(x => x.Id == id && x.Status != 255);
        if (chapter == null) return null;
        var questionCount = await _db.Questions.CountAsync(x => x.ChapterId == id);
        if (chapter.SubjectId != request.SubjectId)
        {
            // Đổi môn khi đã có câu gắn sẽ làm câu hỏi "chương khác môn"
            if (questionCount > 0) throw new ConflictException("Chương đã có câu hỏi, không thể chuyển sang môn khác.");
            await EnsureSubjectAsync(request.SubjectId);
        }
        await EnsureUniqueAsync(request.SubjectId, name, id);
        chapter.SubjectId = request.SubjectId; chapter.Name = name; chapter.SortOrder = request.SortOrder;
        chapter.Status = request.Status ?? chapter.Status; chapter.UpdatedBy = actor; chapter.UpdatedAt = DateTime.UtcNow;
        await SaveAsync();
        return Map(chapter, questionCount);
    }

    public async Task<bool> HideAsync(int id, Guid? actor)
    {
        var chapter = await _db.Chapters.FirstOrDefaultAsync(x => x.Id == id && x.Status != 255);
        if (chapter == null) return false;
        // Câu hỏi đang gắn giữ nguyên ChapterId; chỉ không gắn mới được
        chapter.Status = 255; chapter.UpdatedBy = actor; chapter.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static string Validate(ChapterRequest request)
    {
        if (request.SubjectId <= 0) throw new BadRequestException("Môn phải là ID dương.");
        var name = string.Join(' ', (request.Name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length is < 1 or > 200) throw new BadRequestException("Tên chương phải có từ 1 đến 200 ký tự.");
        if (request.Status is not null and not 0 and not 1) throw new BadRequestException("Trạng thái chỉ nhận 0 hoặc 1.");
        if (request.SortOrder < 0) throw new BadRequestException("Thứ tự không được âm.");
        return name;
    }

    private async Task EnsureSubjectAsync(int subjectId)
    {
        if (!await _db.Subjects.AnyAsync(x => x.Id == subjectId && x.Status == 1))
            throw new BadRequestException("Môn không tồn tại hoặc đã ngừng sử dụng.");
    }

    private async Task EnsureUniqueAsync(int subjectId, string name, int? exceptId)
    {
        if (await _db.Chapters.AnyAsync(x => x.SubjectId == subjectId && x.Name == name && x.Status != 255 && x.Id != exceptId))
            throw new ConflictException("Môn đã có chương cùng tên.");
    }

    // Hai request tạo cùng tên đồng thời: unique index lọc (SubjectId, Name) chặn bản thứ hai
    private async Task SaveAsync()
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new ConflictException("Môn đã có chương cùng tên."); }
    }

    private static ChapterResponse Map(Chapter x, int questionCount) =>
        new(x.Id, x.SubjectId, x.Name, x.SortOrder, x.Status, questionCount);
}
