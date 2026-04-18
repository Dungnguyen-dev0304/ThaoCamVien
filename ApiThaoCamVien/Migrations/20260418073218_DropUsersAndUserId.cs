using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class DropUsersAndUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_visit_user",
                table: "poi_visit_history");

            migrationBuilder.DropForeignKey(
                name: "FK_location_user",
                table: "user_location_log");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_location_log_user_id",
                table: "user_location_log");

            migrationBuilder.DropIndex(
                name: "IX_poi_visit_history_UserId",
                table: "poi_visit_history");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "user_location_log");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "poi_visit_history");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "user_location_log",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "poi_visit_history",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__users__B9BE370F89EDFDDA", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_location_log_user_id",
                table: "user_location_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_poi_visit_history_UserId",
                table: "poi_visit_history",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_visit_user",
                table: "poi_visit_history",
                column: "UserId",
                principalTable: "users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_location_user",
                table: "user_location_log",
                column: "user_id",
                principalTable: "users",
                principalColumn: "UserId");
        }
    }
}
