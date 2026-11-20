using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToBeDebtPracticeSweepIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_OpenDeadline",
                table: "PracticeSessions",
                column: "DeadlineUtc",
                filter: "[State] = 'inProgress' AND [DeadlineUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PracticeSessions_OpenDeadline",
                table: "PracticeSessions");
        }
    }
}
