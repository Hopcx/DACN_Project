using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToBeT16ShortAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShortAnswerIgnoreDiacritics",
                table: "Questions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShortAnswerNumeric",
                table: "Questions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortAnswerTolerance",
                table: "Questions",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "QuestionOutcomes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_ShortAnswerTolerance",
                table: "Questions",
                sql: "[ShortAnswerTolerance] IS NULL OR ([ShortAnswerTolerance] >= 0 AND [ShortAnswerTolerance] <= 1000000000)");

            // To-be T16 (Q10a): loại câu 4 "Điền ngắn" — chỉ chèn khi chưa có Id 4 (không đổi dữ liệu đã có).
            // Review T16: Id 4 đã có với nghĩa khác (tạo qua API trước đó) → dừng, vì câu của loại đó sẽ bị chấm như điền ngắn.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.QuestionTypes WHERE Id = 4 AND Name <> N'Điền ngắn')
    THROW 50015, N'QuestionTypes Id 4 đã được dùng cho loại khác; cần đổi Id loại đó trước khi áp To-be T16.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.QuestionTypes WHERE Id = 4)
BEGIN
    SET IDENTITY_INSERT dbo.QuestionTypes ON;
    INSERT dbo.QuestionTypes (Id, Name, Description, Status)
    VALUES (4, N'Điền ngắn', N'Học viên nhập câu trả lời ngắn; chấm theo danh sách đáp án chấp nhận', 1);
    SET IDENTITY_INSERT dbo.QuestionTypes OFF;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không rollback khi đã có câu điền ngắn (tránh mất dữ liệu câu hỏi)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.Questions WHERE QuestionTypeId = 4)
    THROW 50016, N'Còn câu hỏi loại 4 (Điền ngắn); không thể rollback To-be T16.', 1;
DELETE dbo.QuestionTypes WHERE Id = 4 AND Name = N'Điền ngắn';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_ShortAnswerTolerance",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ShortAnswerIgnoreDiacritics",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ShortAnswerNumeric",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ShortAnswerTolerance",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "QuestionOutcomes");
        }
    }
}
