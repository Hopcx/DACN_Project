using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task10TimeoutAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TimeTakenSeconds",
                table: "ExamAttemptSubmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "PassMark",
                table: "ExamAttempts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.ExamAttempts) THROW 51111, 'Restore backup to remove Task10 duration and score provenance', 1;");
            migrationBuilder.DropColumn(
                name: "TimeTakenSeconds",
                table: "ExamAttemptSubmissions");

            migrationBuilder.AlterColumn<decimal>(
                name: "PassMark",
                table: "ExamAttempts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");
        }
    }
}
