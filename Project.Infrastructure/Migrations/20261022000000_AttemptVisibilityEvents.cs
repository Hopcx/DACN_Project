using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

namespace Project.Infrastructure.Migrations;

[DbContext(typeof(ProjectDACNDbContext))]
[Migration("20261022000000_AttemptVisibilityEvents")]
public class AttemptVisibilityEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttemptVisibilityEvents",
            columns: table => new
            {
                AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsCounted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttemptVisibilityEvents", x => new { x.AttemptId, x.EventId });
                table.ForeignKey("FK_AttemptVisibilityEvents_ExamAttempts_AttemptId", x => x.AttemptId,
                    "ExamAttempts", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_AttemptVisibilityEvents_AttemptId_RecordedAtUtc",
            "AttemptVisibilityEvents", new[] { "AttemptId", "RecordedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.AttemptVisibilityEvents) THROW 51121, 'Remove visibility fixtures before downgrade', 1;");
        migrationBuilder.DropTable("AttemptVisibilityEvents");
    }
}
