using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Project.Api.Services;
using Project.Infrastructure.Persistence;

const string copyName = "ProjectDACN_Task04Task09_Test";
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? throw new InvalidOperationException("Explicit copy connection required.");
var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
if (!string.Equals(builder.InitialCatalog, copyName, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Only the named SQL Server copy may be used.");

var services = new ServiceCollection();
services.AddLogging(x => x.AddConsole());
services.AddDbContext<ProjectDACNDbContext>(x => x.UseSqlServer(connection));
services.AddScoped<ScheduleWriteGuard>();
services.AddScoped<AttemptFinalizer>();
await using var provider = services.BuildServiceProvider();
if (args.Length == 3 && args[0] == "--hold-lock" &&
    int.TryParse(args[2], out var holdMilliseconds) && holdMilliseconds is > 0 and <= 15000)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProjectDACNDbContext>();
    var guard = scope.ServiceProvider.GetRequiredService<ScheduleWriteGuard>();
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    await guard.AcquireAsync();
    File.WriteAllText(args[1], DateTime.UtcNow.ToString("O"));
    await Task.Delay(holdMilliseconds);
    await transaction.CommitAsync();
    Console.WriteLine("COPY_DB_TEST_LOCK_RELEASED");
    return;
}
var worker = ActivatorUtilities.CreateInstance<ExpiredAttemptFinalizerService>(provider);
var count = await worker.SweepOnceAsync();
Console.WriteLine($"WORKER_SWEEP_COMPLETED={count}");
