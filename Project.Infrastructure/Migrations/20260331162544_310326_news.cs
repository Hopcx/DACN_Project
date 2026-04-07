using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _310326_news : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111114"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "AvatarUrl", "DateOfBirth", "Email", "FullName", "LastLogin", "LevelId", "PasswordHash", "PhoneNumber", "Sex", "Status", "UserName" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111112"), "A", null, new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "abcde@gmail.com", "Nguyen Van A", null, 4, "4297f44b13955235245b2497399d7a93", "0987654321", false, (byte)1, "nva" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "AvatarUrl", "DateOfBirth", "Email", "FullName", "LastLogin", "LevelId", "PasswordHash", "PhoneNumber", "Sex", "Status", "UserName" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "A", null, new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "abcde@gmail.com", "Nguyen Van A", null, 4, "4297f44b13955235245b2497399d7a93", "0987654321", false, (byte)1, "nva" });
        }
    }
}
