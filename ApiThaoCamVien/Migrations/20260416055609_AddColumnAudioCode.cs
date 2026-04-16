using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnAudioCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PoiAudios_pois_PoiId",
                table: "PoiAudios");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "poi_translations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddForeignKey(
                name: "FK_PoiAudios_Pois",
                table: "PoiAudios",
                column: "PoiId",
                principalTable: "pois",
                principalColumn: "poi_id",
                onDelete: ReferentialAction.Cascade);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PoiAudios_Pois",
                table: "PoiAudios");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "poi_translations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PoiAudios_pois_PoiId",
                table: "PoiAudios",
                column: "PoiId",
                principalTable: "pois",
                principalColumn: "poi_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
