using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "queue_tickets",
                columns: table => new
                {
                    ticket_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    poi_id = table.Column<int>(type: "int", nullable: false),
                    session_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    joined_utc = table.Column<DateTime>(type: "datetime", nullable: false),
                    started_playing_utc = table.Column<DateTime>(type: "datetime", nullable: true),
                    finished_utc = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_tickets", x => x.ticket_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_queue_tickets_joined",
                table: "queue_tickets",
                column: "joined_utc");

            migrationBuilder.CreateIndex(
                name: "IX_queue_tickets_poi_active",
                table: "queue_tickets",
                columns: new[] { "poi_id", "finished_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "queue_tickets");
        }
    }
}
