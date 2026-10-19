using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

#nullable disable
namespace Project.Infrastructure.Migrations;

[DbContext(typeof(ProjectDACNDbContext))]
[Migration("20261019090000_Task12LegacyProjection")]
public sealed class Task12LegacyProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("LegacyImportBatches", table => new {
            Id = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceSystem = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false),
            SourceDatabase = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false),
            SourceMigration = table.Column<string>("nvarchar(150)", maxLength: 150, nullable: false),
            Marker = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            ImportedAtUtc = table.Column<DateTime>("datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_LegacyImportBatches", x => x.Id));
        migrationBuilder.CreateTable("LegacyUserMaps", table => new {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BatchId = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceUserId = table.Column<Guid>("uniqueidentifier", nullable: false),
            TargetUserId = table.Column<Guid>("uniqueidentifier", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_LegacyUserMaps", x => x.Id);
            table.ForeignKey("FK_LegacyUserMaps_LegacyImportBatches_BatchId", x => x.BatchId, "LegacyImportBatches", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_LegacyUserMaps_Users_TargetUserId", x => x.TargetUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("LegacySubmissionRecords", table => new {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BatchId = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceTable = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false),
            SourceId = table.Column<int>("int", nullable: false),
            SourceUserId = table.Column<Guid>("uniqueidentifier", nullable: false),
            TargetUserId = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceExamId = table.Column<int>("int", nullable: false),
            SourceExamDetailId = table.Column<int>("int", nullable: false),
            SourceScheduleId = table.Column<int>("int", nullable: false),
            ExamName = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            ScheduleTitle = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: true),
            SubmitTimeUnknown = table.Column<DateTime>("datetime2", nullable: false),
            TimeTaken = table.Column<TimeOnly>("time", nullable: false),
            TotalMark = table.Column<double>("float", nullable: false),
            IsPassed = table.Column<bool>("bit", nullable: false),
            LegacyStatus = table.Column<bool>("bit", nullable: true),
            AllowViewResult = table.Column<bool>("bit", nullable: false),
            Answered = table.Column<int>("int", nullable: false),
            UnAnswered = table.Column<int>("int", nullable: false),
            SourceChecksum = table.Column<byte[]>("binary(32)", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_LegacySubmissionRecords", x => x.Id);
            table.ForeignKey("FK_LegacySubmissionRecords_LegacyImportBatches_BatchId", x => x.BatchId, "LegacyImportBatches", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_LegacySubmissionRecords_Users_TargetUserId", x => x.TargetUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("LegacyAnswerSelections", table => new {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            LegacySubmissionRecordId = table.Column<long>("bigint", nullable: false),
            SourceTable = table.Column<string>("nvarchar(80)", maxLength: 80, nullable: false),
            SourceId = table.Column<int>("int", nullable: false),
            SourceQuestionId = table.Column<int>("int", nullable: false),
            SourceAnswerId = table.Column<int>("int", nullable: false),
            SourceChecksum = table.Column<byte[]>("binary(32)", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_LegacyAnswerSelections", x => x.Id);
            table.ForeignKey("FK_LegacyAnswerSelections_LegacySubmissionRecords_LegacySubmissionRecordId", x => x.LegacySubmissionRecordId, "LegacySubmissionRecords", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_LegacyImportBatches_SourceSystem_SourceDatabase_Marker", "LegacyImportBatches", new[] { "SourceSystem", "SourceDatabase", "Marker" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacyUserMaps_BatchId_SourceUserId", "LegacyUserMaps", new[] { "BatchId", "SourceUserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacyUserMaps_TargetUserId", "LegacyUserMaps", "TargetUserId");
        migrationBuilder.CreateIndex("IX_LegacySubmissionRecords_BatchId_SourceTable_SourceId", "LegacySubmissionRecords", new[] { "BatchId", "SourceTable", "SourceId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacySubmissionRecords_TargetUserId", "LegacySubmissionRecords", "TargetUserId");
        migrationBuilder.CreateIndex("IX_LegacyAnswerSelections_LegacySubmissionRecordId_SourceTable_SourceId", "LegacyAnswerSelections", new[] { "LegacySubmissionRecordId", "SourceTable", "SourceId" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("LegacyAnswerSelections");
        migrationBuilder.DropTable("LegacyUserMaps");
        migrationBuilder.DropTable("LegacySubmissionRecords");
        migrationBuilder.DropTable("LegacyImportBatches");
    }
}
