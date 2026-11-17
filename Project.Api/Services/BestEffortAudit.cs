using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

// To-be T2 (QT2-4 a): ghi audit SAU khi thao tác chính đã lưu, SaveChanges riêng.
// Lỗi ghi audit chỉ log kỹ thuật, không làm hỏng request. Mô tả không chứa nội dung câu hỏi/đáp án.
public sealed class BestEffortAudit
{
    private readonly AuditWriter _writer;
    private readonly ProjectDACNDbContext _db;
    private readonly ILogger<BestEffortAudit> _logger;

    public BestEffortAudit(AuditWriter writer, ProjectDACNDbContext db, ILogger<BestEffortAudit> logger)
    { _writer = writer; _db = db; _logger = logger; }

    public async Task WriteAsync(ClaimsPrincipal user, string action, string entityType, string? entityId, string description)
    {
        try
        {
            _writer.Add(user, action, entityType, entityId, description);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không ghi được audit {Action} cho {EntityType} {EntityId}", action, entityType, entityId);
            // Bỏ dòng Logs chưa lưu được để lần SaveChanges sau của request không lỗi theo
            foreach (var entry in _db.ChangeTracker.Entries<Log>().Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }
}
