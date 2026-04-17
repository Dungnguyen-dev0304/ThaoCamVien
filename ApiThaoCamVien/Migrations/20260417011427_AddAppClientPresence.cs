using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddAppClientPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_client_presence",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    last_seen_utc = table.Column<DateTime>(type: "datetime", nullable: false),
                    current_poi_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_client_presence", x => x.session_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_client_presence");
        }
    }
}
