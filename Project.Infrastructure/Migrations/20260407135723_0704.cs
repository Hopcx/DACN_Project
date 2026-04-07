using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _0704 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPermissions_Logs_LogId",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_LogId",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "LogId",
                table: "UserPermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LogId",
                table: "UserPermissions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_LogId",
                table: "UserPermissions",
                column: "LogId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermissions_Logs_LogId",
                table: "UserPermissions",
                column: "LogId",
                principalTable: "Logs",
                principalColumn: "Id");
        }
    }
}
