using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikrotikScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PppoeExpiryNoPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppoeUsers_RouterId_IsActive",
                table: "PppoeUsers");

            migrationBuilder.DropColumn(
                name: "PasswordEncrypted",
                table: "PppoeUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "PppoeUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PppoeUsers_RouterId_IsActive_ExpiresAt",
                table: "PppoeUsers",
                columns: new[] { "RouterId", "IsActive", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppoeUsers_RouterId_IsActive_ExpiresAt",
                table: "PppoeUsers");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PppoeUsers");

            migrationBuilder.AddColumn<string>(
                name: "PasswordEncrypted",
                table: "PppoeUsers",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PppoeUsers_RouterId_IsActive",
                table: "PppoeUsers",
                columns: new[] { "RouterId", "IsActive" });
        }
    }
}
