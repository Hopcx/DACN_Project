using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

const string copy = "ProjectDACN_Task04Task09_Test";
const string previous = "20261019090000_Task11AttemptVoidAudit";
const string next = "20261019090000_Task12LegacyProjection";
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? throw new InvalidOperationException("Explicit copy connection required.");
if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, copy, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Only the named SQL Server copy may be used.");
await using var db = new ProjectDACNDbContext(new DbContextOptionsBuilder<ProjectDACNDbContext>().UseSqlServer(connection).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME()";
    if (!string.Equals((string?)await command.ExecuteScalarAsync(), copy, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("DB_NAME preflight failed.");
}
var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
Console.WriteLine($"DB_NAME={copy}; APPLIED={applied.Count}; PENDING={string.Join(',', pending)}");
if (args.Length != 1 || args[0] is not ("--script" or "--apply" or "--compare")) throw new ArgumentException("Use --script, --apply, or --compare.");
if (args[0] == "--compare")
{
    if (pending.Count != 0 || applied.LastOrDefault() != next) throw new InvalidOperationException("Migration not applied on copy.");
    Console.WriteLine($"PENDING_MODEL_CHANGES={db.Database.HasPendingModelChanges()}");
    return;
}
if (pending.Count != 1 || pending[0] != next || applied.LastOrDefault() != previous)
    throw new InvalidOperationException("Unexpected migration history; refusing write/script.");
var migrator = db.GetService<IMigrator>();
if (args[0] == "--script") { Console.WriteLine(migrator.GenerateScript(previous, next)); return; }
await db.Database.MigrateAsync();
Console.WriteLine($"APPLIED={next}");
