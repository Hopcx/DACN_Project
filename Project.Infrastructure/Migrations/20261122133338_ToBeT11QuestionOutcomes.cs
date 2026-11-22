using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToBeT11QuestionOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionOutcomes",
                columns: table => new
                {
                    SourceKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    ExamScheduleId = table.Column<int>(type: "int", nullable: true),
                    SourceQuestionId = table.Column<int>(type: "int", nullable: false),
                    ChapterId = table.Column<int>(type: "int", nullable: true),
                    TagIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    SelectedAnswerIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClassificationSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOutcomes", x => new { x.SourceKind, x.SourceId, x.SnapshotQuestionId });
                    table.CheckConstraint("CK_QuestionOutcomes_Classification", "[ClassificationSource] IN ('snapshot','current')");
                    table.CheckConstraint("CK_QuestionOutcomes_SelectedJson", "ISJSON([SelectedAnswerIdsJson]) = 1");
                    table.CheckConstraint("CK_QuestionOutcomes_SourceKind", "[SourceKind] IN ('exam','practice','mock')");
                    table.CheckConstraint("CK_QuestionOutcomes_TagIdsJson", "ISJSON([TagIdsJson]) = 1");
                    table.ForeignKey(
                        name: "FK_QuestionOutcomes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOutcomes_Schedule",
                table: "QuestionOutcomes",
                column: "ExamScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOutcomes_User_Subject_Finished",
                table: "QuestionOutcomes",
                columns: new[] { "UserId", "SubjectId", "FinishedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "SourceKind", "SourceId", "ChapterId", "TagIdsJson", "IsCorrect", "ClassificationSource" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionOutcomes");
        }
    }
}
