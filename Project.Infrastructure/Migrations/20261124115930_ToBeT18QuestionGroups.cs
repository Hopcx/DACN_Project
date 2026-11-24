using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToBeT18QuestionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupOrder",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionGroups_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionGroupMedia",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionGroupMedia", x => new { x.GroupId, x.MediaId });
                    table.ForeignKey(
                        name: "FK_QuestionGroupMedia_MediaFiles_MediaId",
                        column: x => x.MediaId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionGroupMedia_QuestionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "QuestionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Group",
                table: "Questions",
                columns: new[] { "GroupId", "GroupOrder" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Group",
                table: "Questions",
                sql: "([GroupId] IS NULL AND [GroupOrder] IS NULL) OR ([GroupId] IS NOT NULL AND [GroupOrder] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionGroupMedia_Media",
                table: "QuestionGroupMedia",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionGroups_Subject_Status",
                table: "QuestionGroups",
                columns: new[] { "SubjectId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_QuestionGroups_GroupId",
                table: "Questions",
                column: "GroupId",
                principalTable: "QuestionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // To-be T18: không rollback khi đã có đoạn dùng chung (tránh mất đoạn văn và thứ tự câu con)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.QuestionGroups)
    THROW 50019, N'Còn đoạn dùng chung (QuestionGroups); không thể rollback To-be T18.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_QuestionGroups_GroupId",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "QuestionGroupMedia");

            migrationBuilder.DropTable(
                name: "QuestionGroups");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Group",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Group",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "GroupOrder",
                table: "Questions");
        }
    }
}
