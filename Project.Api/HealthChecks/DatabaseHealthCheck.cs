using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Project.Infrastructure.Persistence;

namespace Project.Api.HealthChecks;

/// <summary>Readiness: SQL Server kết nối được qua DbContext. Không trả chi tiết lỗi hay chuỗi kết nối.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ProjectDACNDbContext _db;

    public DatabaseHealthCheck(ProjectDACNDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception)
        {
            // Lỗi chi tiết đã có trong log của EF; response chỉ báo trạng thái
            return HealthCheckResult.Unhealthy();
        }
    }
}
