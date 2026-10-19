using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

#nullable disable

namespace Project.Infrastructure.Migrations;

[DbContext(typeof(ProjectDACNDbContext))]
[Migration("20261019090000_Task11AttemptVoidAudit")]
public sealed class Task11AttemptVoidAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "VoidedAtUtc", table: "ExamAttemptSubmissions", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "VoidedByUserId", table: "ExamAttemptSubmissions", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "VoidReason", table: "ExamAttemptSubmissions", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_ExamAttemptSubmissions_VoidedByUserId", table: "ExamAttemptSubmissions", column: "VoidedByUserId");
        migrationBuilder.AddForeignKey(
            name: "FK_ExamAttemptSubmissions_Users_VoidedByUserId", table: "ExamAttemptSubmissions",
            column: "VoidedByUserId", principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.ExamAttemptSubmissions WHERE VoidedAtUtc IS NOT NULL) THROW 51112, 'Restore backup to remove attempt void audit', 1;");
        migrationBuilder.DropForeignKey(name: "FK_ExamAttemptSubmissions_Users_VoidedByUserId", table: "ExamAttemptSubmissions");
        migrationBuilder.DropIndex(name: "IX_ExamAttemptSubmissions_VoidedByUserId", table: "ExamAttemptSubmissions");
        migrationBuilder.DropColumn(name: "VoidedAtUtc", table: "ExamAttemptSubmissions");
        migrationBuilder.DropColumn(name: "VoidedByUserId", table: "ExamAttemptSubmissions");
        migrationBuilder.DropColumn(name: "VoidReason", table: "ExamAttemptSubmissions");
    }
}
