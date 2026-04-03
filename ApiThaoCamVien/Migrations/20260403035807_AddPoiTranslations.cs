using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poi_translations",
                columns: table => new
                {
                    translation_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    poi_id = table.Column<int>(type: "int", nullable: false),
                    language_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poi_translations", x => x.translation_id);
                    table.ForeignKey(
                        name: "FK_translations_poi",
                        column: x => x.poi_id,
                        principalTable: "pois",
                        principalColumn: "poi_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_poi_translations_poi_id",
                table: "poi_translations",
                column: "poi_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poi_translations");
        }
    }
}
