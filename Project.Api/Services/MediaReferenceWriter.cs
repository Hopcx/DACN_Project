using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

/// <summary>To-be T15 (Q11c): thêm MediaReferences cho media trong snapshot vừa chụp (chưa SaveChanges — cùng transaction).</summary>
public static class MediaReferenceWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Review T15: khóa dòng MediaFiles (UPDLOCK, HOLDLOCK tới hết transaction) rồi mới ghi tham chiếu — Admin xóa media mồ côi
    /// cũng khóa dòng đó nên hai bên tuần tự: Admin xóa trước thì media đã mất bị bỏ qua (không vi phạm FK → không 500);
    /// tạo bài trước thì Admin thấy tham chiếu và nhận MEDIA_IN_USE. Phải gọi bên trong transaction của nơi tạo snapshot.
    /// </summary>
    public static async Task<int> AddAsync(ProjectDACNDbContext db, string kind, Guid sourceId, AttemptSnapshot snapshot, CancellationToken ct = default)
    {
        var ids = snapshot.Questions.SelectMany(q => q.Media ?? []).Select(m => m.Id).Distinct().ToList();
        if (ids.Count == 0) return 0;
        var idsJson = JsonSerializer.Serialize(ids);
        var existing = await db.MediaFiles
            .FromSqlInterpolated($"SELECT * FROM dbo.MediaFiles WITH (UPDLOCK, HOLDLOCK) WHERE Id IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON({idsJson}))")
            .AsNoTracking().Select(x => x.Id).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var kept = ids.Where(existing.Contains).ToList();
        db.MediaReferences.AddRange(kept.Select(id => new MediaReference { MediaId = id, SourceKind = kind, SourceId = sourceId, CreatedAtUtc = now }));
        return kept.Count;
    }

    public static Task<int> AddFromJsonAsync(ProjectDACNDbContext db, string kind, Guid sourceId, string snapshotJson, CancellationToken ct = default) =>
        AddAsync(db, kind, sourceId, JsonSerializer.Deserialize<AttemptSnapshot>(snapshotJson, Json)!, ct);
}

public static class MediaUrlFactory
{
    /// <summary>Hàm ký URL media cho đúng một người dùng (học viên đang xem câu hỏi của chính mình).</summary>
    public static Func<Guid, string> For(MediaSettings settings, Guid userId) => id => Project.Api.Controllers.MediaUrls.Url(settings, id, userId);
}
