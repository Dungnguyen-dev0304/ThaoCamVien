using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddAuidoCodeInPoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Language",
                table: "poi_media",
                newName: "language");

            migrationBuilder.RenameColumn(
                name: "MediaType",
                table: "poi_media",
                newName: "media_type");

            migrationBuilder.AddColumn<string>(
                name: "AudioCode",
                table: "pois",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioCode",
                table: "pois");

            migrationBuilder.RenameColumn(
                name: "language",
                table: "poi_media",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "media_type",
                table: "poi_media",
                newName: "MediaType");
        }
    }
}
