using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class SyncFromFriendCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "user_location_log",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "user_location_log",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "RecordedAt",
                table: "user_location_log",
                newName: "recorded_at");

            migrationBuilder.RenameIndex(
                name: "IX_user_location_log_UserId",
                table: "user_location_log",
                newName: "IX_user_location_log_user_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "recorded_at",
                table: "user_location_log",
                type: "datetime",
                nullable: true,
                defaultValueSql: "(getdate())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "id",
                table: "user_location_log",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "user_location_log",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "recorded_at",
                table: "user_location_log",
                newName: "RecordedAt");

            migrationBuilder.RenameIndex(
                name: "IX_user_location_log_user_id",
                table: "user_location_log",
                newName: "IX_user_location_log_UserId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RecordedAt",
                table: "user_location_log",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true,
                oldDefaultValueSql: "(getdate())");
        }
    }
}
