using System.Security.Claims;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

// Ghi một dòng audit vào bảng Logs. Chỉ Add vào DbContext, không SaveChanges:
// caller lưu trong transaction của chính mình.
public sealed class AuditWriter
{
    private readonly ProjectDACNDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditWriter(ProjectDACNDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public void Add(ClaimsPrincipal user, string action, string entityType, string? entityId, string description)
    {
        var actor = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        _db.Logs.Add(new Log
        {
            Name = action,
            Description = description.Length > 1000 ? description[..1000] : description,
            Status = 1,
            ActorUserId = actor,
            CreatedAtUtc = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            CorrelationId = _http.HttpContext?.TraceIdentifier
        });
    }
}
