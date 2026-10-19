using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

const string copyName = "ProjectDACN_Task04Task09_Test";
const string lastMigration = "20261019090000_Task11AttemptVoidAudit";
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? throw new InvalidOperationException("Explicit copy connection required.");
var builder = new SqlConnectionStringBuilder(connection);
if (!string.Equals(builder.InitialCatalog, copyName, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Only the named SQL Server copy may be used.");
await using var db = new ProjectDACNDbContext(new DbContextOptionsBuilder<ProjectDACNDbContext>()
    .UseSqlServer(connection).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME()";
    if (!string.Equals((string?)await command.ExecuteScalarAsync(), copyName, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("DB_NAME preflight failed.");
}
var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
Console.WriteLine($"DB_NAME={copyName}; APPLIED={applied.Count}; PENDING={string.Join(',', pending)}");
if (args.Length != 1 || args[0] is not ("--script" or "--apply"))
    throw new ArgumentException("Use --script or --apply.");
if (pending.Count != 1 || pending[0] != lastMigration ||
    applied.LastOrDefault() != "20261018063239_Task10TimeoutAndDuration")
    throw new InvalidOperationException("Unexpected migration history; refusing write/script.");
var migrator = db.GetService<IMigrator>();
if (args[0] == "--script")
{
    Console.WriteLine(migrator.GenerateScript(applied.Last(), lastMigration));
    return;
}
await db.Database.MigrateAsync();
Console.WriteLine($"APPLIED={lastMigration}");
