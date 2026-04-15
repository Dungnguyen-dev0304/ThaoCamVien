using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiAudioTableAndAudioCode : Migration
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

            migrationBuilder.AlterColumn<string>(
                name: "image_thumbnail",
                table: "pois",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldUnicode: false,
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "pois",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "audio_code",
                table: "pois",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_code",
                table: "pois");

            migrationBuilder.RenameColumn(
                name: "language",
                table: "poi_media",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "media_type",
                table: "poi_media",
                newName: "MediaType");

            migrationBuilder.AlterColumn<string>(
                name: "image_thumbnail",
                table: "pois",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldUnicode: false,
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "pois",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
