using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task09ScheduleUtcProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTimeUtc",
                table: "ExamSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.ExamSchedules WHERE IsTimeUtc = 1)
                    THROW 51014, 'Cannot remove UTC provenance while UTC schedules exist', 1;
                """);
            migrationBuilder.DropColumn(
                name: "IsTimeUtc",
                table: "ExamSchedules");
        }
    }
}
