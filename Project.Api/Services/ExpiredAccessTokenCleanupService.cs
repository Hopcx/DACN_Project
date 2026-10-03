using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

public class ExpiredAccessTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpiredAccessTokenCleanupService> _logger;

    public ExpiredAccessTokenCleanupService(IServiceScopeFactory scopes, ILogger<ExpiredAccessTokenCleanupService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
                await db.BlackListTokens.Where(x => x.ExpiryDate < DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Could not prune expired access jtis");
            }
        }
    }
}
