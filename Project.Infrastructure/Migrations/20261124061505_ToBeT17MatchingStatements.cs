using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToBeT17MatchingStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // To-be T17: vế phải của câu ghép cặp (loại 5); loại khác NULL
            migrationBuilder.AddColumn<string>(
                name: "MatchContent",
                table: "Answers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            // To-be T17: loại 5 "Ghép cặp", 6 "Đúng/Sai nhiều mệnh đề" — chỉ chèn khi chưa có; Id đã dùng cho loại khác → dừng
            // (câu của loại đó sẽ bị chấm sai cách), giống T16.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.QuestionTypes WHERE (Id = 5 AND Name <> N'Ghép cặp') OR (Id = 6 AND Name <> N'Đúng/Sai nhiều mệnh đề'))
    THROW 50017, N'QuestionTypes Id 5 hoặc 6 đã được dùng cho loại khác; cần đổi Id loại đó trước khi áp To-be T17.', 1;
SET IDENTITY_INSERT dbo.QuestionTypes ON;
IF NOT EXISTS (SELECT 1 FROM dbo.QuestionTypes WHERE Id = 5)
    INSERT dbo.QuestionTypes (Id, Name, Description, Status)
    VALUES (5, N'Ghép cặp', N'Ghép mỗi vế trái với một vế phải; đúng khi ghép đúng tất cả các cặp', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.QuestionTypes WHERE Id = 6)
    INSERT dbo.QuestionTypes (Id, Name, Description, Status)
    VALUES (6, N'Đúng/Sai nhiều mệnh đề', N'Chọn Đúng hoặc Sai cho từng mệnh đề; đúng khi chọn đúng tất cả mệnh đề', 1);
SET IDENTITY_INSERT dbo.QuestionTypes OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không rollback khi đã có câu loại 5/6 (tránh mất dữ liệu câu hỏi); chỉ xóa đúng các dòng migration đã chèn
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.Questions WHERE QuestionTypeId IN (5, 6))
    THROW 50018, N'Còn câu hỏi loại 5/6 (Ghép cặp, Đúng/Sai nhiều mệnh đề); không thể rollback To-be T17.', 1;
DELETE dbo.QuestionTypes WHERE (Id = 5 AND Name = N'Ghép cặp') OR (Id = 6 AND Name = N'Đúng/Sai nhiều mệnh đề');");

            migrationBuilder.DropColumn(
                name: "MatchContent",
                table: "Answers");
        }
    }
}
