using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task09ExamScheduleDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old FK direction cannot be reconstructed after dropping this column.
            // Stop instead of discarding a link or blessing an orphan on a database
            // that changed since the read-only preflight.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Exams] WHERE [ExamScheduleId] IS NOT NULL)
                    THROW 51011, 'ExamScheduleId contains old links; reconcile before Task09 migration.', 1;
                IF EXISTS (SELECT 1 FROM [ExamSchedules] s LEFT JOIN [Exams] e ON e.[Id] = s.[ExamId] WHERE e.[Id] IS NULL)
                    THROW 51012, 'ExamSchedules contains orphan ExamId; reconcile before Task09 migration.', 1;
                IF EXISTS (SELECT 1 FROM [DoingExams]) OR EXISTS (SELECT 1 FROM [Submissions])
                    THROW 51013, 'Existing attempts/submissions require manual Task09 review.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_ExamSchedules_ExamScheduleId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_ExamScheduleId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ExamScheduleId",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamId",
                table: "ExamSchedules",
                column: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSchedules_Exams_ExamId",
                table: "ExamSchedules",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One Exam may have many schedules, so Down cannot restore the old
            // single nullable ExamScheduleId once schedules exist.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [ExamSchedules])
                    THROW 51014, 'Restore a reviewed backup to roll back Task09 with schedules.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ExamSchedules_Exams_ExamId",
                table: "ExamSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ExamSchedules_ExamId",
                table: "ExamSchedules");

            migrationBuilder.AddColumn<int>(
                name: "ExamScheduleId",
                table: "Exams",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_ExamScheduleId",
                table: "Exams",
                column: "ExamScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_ExamSchedules_ExamScheduleId",
                table: "Exams",
                column: "ExamScheduleId",
                principalTable: "ExamSchedules",
                principalColumn: "Id");
        }
    }
}
