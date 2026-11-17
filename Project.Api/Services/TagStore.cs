using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Application.Exceptions;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

public sealed record TagRequest(string? Name, byte? Status);
public sealed record TagResponse(int Id, string Name, byte Status, int QuestionCount);

// To-be T2 (Q2, QT2-3 a): tag dùng chung; trùng sau chuẩn hóa (TagName.Normalize) → 409 kèm id tag có sẵn.
public sealed class TagStore
{
    private readonly ProjectDACNDbContext _db;
    public TagStore(ProjectDACNDbContext db) => _db = db;

    public async Task<List<TagResponse>> ListAsync(string? textSearch, bool includeHidden)
    {
        var query = _db.Tags.AsNoTracking();
        if (!includeHidden) query = query.Where(x => x.Status != 255);
        var needle = TagName.Normalize(textSearch);
        if (needle.Length > 0) query = query.Where(x => x.NormalizedName.Contains(needle));
        return await query.OrderBy(x => x.Name)
            .Select(x => new TagResponse(x.Id, x.Name, x.Status, x.QuestionTags.Count)).ToListAsync();
    }

    public async Task<TagResponse> CreateAsync(TagRequest request, Guid? actor)
    {
        var (name, normalized) = Validate(request.Name);
        await EnsureUniqueAsync(normalized, null);
        var tag = new Tag { Name = name, NormalizedName = normalized, Status = 1, CreatedBy = actor, CreatedAt = DateTime.UtcNow };
        _db.Tags.Add(tag);
        await SaveAsync(normalized);
        return new TagResponse(tag.Id, tag.Name, tag.Status, 0);
    }

    public async Task<TagResponse?> UpdateAsync(int id, TagRequest request, Guid? actor)
    {
        var (name, normalized) = Validate(request.Name);
        if (request.Status is not null and not 0 and not 1) throw new BadRequestException("Trạng thái chỉ nhận 0 hoặc 1.");
        // Admin sửa được cả tag đã ẩn: gửi status 0/1 để hiện lại; không gửi status thì giữ trạng thái hiện tại
        var tag = await _db.Tags.FirstOrDefaultAsync(x => x.Id == id);
        if (tag == null) return null;
        await EnsureUniqueAsync(normalized, id);
        tag.Name = name; tag.NormalizedName = normalized; tag.Status = request.Status ?? tag.Status;
        tag.UpdatedBy = actor; tag.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(normalized);
        return new TagResponse(tag.Id, tag.Name, tag.Status, await _db.QuestionTags.CountAsync(x => x.TagId == id));
    }

    public async Task<bool> HideAsync(int id, Guid? actor)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(x => x.Id == id && x.Status != 255);
        if (tag == null) return false;
        // Câu đang gắn giữ liên kết; tag ẩn không gắn mới được
        tag.Status = 255; tag.UpdatedBy = actor; tag.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static (string Name, string Normalized) Validate(string? raw)
    {
        var name = TagName.Clean(raw);
        if (name.Length is < 1 or > TagName.MaxLength)
            throw new BadRequestException($"Tên tag phải có từ 1 đến {TagName.MaxLength} ký tự.");
        return (name, TagName.Normalize(name));
    }

    // Unique NormalizedName tính cả tag đã ẩn: tạo lại tên trùng tag ẩn cũng 409; Admin hiện lại tag cũ bằng PUT status 1
    private async Task EnsureUniqueAsync(string normalized, int? exceptId)
    {
        var existing = await _db.Tags.AsNoTracking().Where(x => x.NormalizedName == normalized && x.Id != exceptId)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (existing.HasValue) throw new ConflictException($"Tag đã tồn tại (id {existing.Value}).");
    }

    private async Task SaveAsync(string normalized)
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            foreach (var entry in _db.ChangeTracker.Entries<Tag>().Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            var id = await _db.Tags.AsNoTracking().Where(x => x.NormalizedName == normalized).Select(x => x.Id).FirstOrDefaultAsync();
            throw new ConflictException($"Tag đã tồn tại (id {id}).");
        }
    }
}
