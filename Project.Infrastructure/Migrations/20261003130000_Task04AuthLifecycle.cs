using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Infrastructure.Persistence;

namespace Project.Infrastructure.Migrations;

[DbContext(typeof(ProjectDACNDbContext))]
[Migration("20261003130000_Task04AuthLifecycle")]
public class Task04AuthLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_PhoneNumber", table: "Users");
        migrationBuilder.AddColumn<DateTime>(name: "EmailVerifiedAt", table: "Users", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "LastVerificationSentAt", table: "Users", type: "datetime2", nullable: true);
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", table: "Users", type: "nvarchar(450)", nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(450)");
        migrationBuilder.CreateIndex(name: "IX_Users_PhoneNumber", table: "Users", column: "PhoneNumber",
            unique: true, filter: "[PhoneNumber] IS NOT NULL");

        // Existing JSON refresh tokens cannot be accepted by the new cookie contract.
        // Hash their stored values and revoke them before renaming the column.
        migrationBuilder.Sql("UPDATE [RefreshTokens] SET [Token] = CONVERT(nvarchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), [Token])), 2), [IsRevoked] = 1;");
        migrationBuilder.RenameColumn(name: "Token", table: "RefreshTokens", newName: "TokenHash");
        migrationBuilder.AlterColumn<string>(name: "TokenHash", table: "RefreshTokens", type: "nvarchar(64)",
            maxLength: 64, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(550)", oldMaxLength: 550);
        migrationBuilder.AddColumn<Guid>(name: "FamilyId", table: "RefreshTokens", type: "uniqueidentifier",
            nullable: false, defaultValueSql: "NEWID()");
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_TokenHash", table: "RefreshTokens", column: "TokenHash");
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_FamilyId_IsRevoked", table: "RefreshTokens",
            columns: new[] { "FamilyId", "IsRevoked" });

        migrationBuilder.CreateTable(name: "BlackListTokens", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            Token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
            ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
            BlacklistAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_BlackListTokens", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_BlackListTokens_Token", table: "BlackListTokens", column: "Token", unique: true);
        migrationBuilder.CreateIndex(name: "IX_BlackListTokens_ExpiryDate", table: "BlackListTokens", column: "ExpiryDate");

        migrationBuilder.CreateTable(name: "EmailVerificationTokens", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_EmailVerificationTokens", x => x.Id);
            table.ForeignKey(name: "FK_EmailVerificationTokens_Users_UserId", column: x => x.UserId,
                principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex(name: "IX_EmailVerificationTokens_TokenHash", table: "EmailVerificationTokens",
            column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_EmailVerificationTokens_UserId_CreatedAt", table: "EmailVerificationTokens",
            columns: new[] { "UserId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmailVerificationTokens");
        migrationBuilder.DropTable(name: "BlackListTokens");
        migrationBuilder.DropIndex(name: "IX_RefreshTokens_FamilyId_IsRevoked", table: "RefreshTokens");
        migrationBuilder.DropIndex(name: "IX_RefreshTokens_TokenHash", table: "RefreshTokens");
        migrationBuilder.DropColumn(name: "FamilyId", table: "RefreshTokens");
        migrationBuilder.RenameColumn(name: "TokenHash", table: "RefreshTokens", newName: "Token");
        migrationBuilder.AlterColumn<string>(name: "Token", table: "RefreshTokens", type: "nvarchar(550)",
            maxLength: 550, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(64)", oldMaxLength: 64);
        migrationBuilder.DropIndex(name: "IX_Users_PhoneNumber", table: "Users");
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Users] WHERE [PhoneNumber] IS NULL) THROW 51000, 'Rollback requires a phone number for every user', 1;");
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", table: "Users", type: "nvarchar(450)",
            nullable: false, oldClrType: typeof(string), oldType: "nvarchar(450)", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Users_PhoneNumber", table: "Users", column: "PhoneNumber", unique: true);
        migrationBuilder.DropColumn(name: "EmailVerifiedAt", table: "Users");
        migrationBuilder.DropColumn(name: "LastVerificationSentAt", table: "Users");
        // Down cannot recover plaintext refresh tokens or verification records. Restore a backup if needed.
    }
}
