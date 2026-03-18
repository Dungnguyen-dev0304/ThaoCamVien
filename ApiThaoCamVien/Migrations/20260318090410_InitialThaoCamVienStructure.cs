using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiThaoCamVien.Migrations
{
    /// <inheritdoc />
    public partial class InitialThaoCamVienStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poi_category",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    category_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__poi_cate__D54EE9B48CAB2166", x => x.category_id);
                });

            migrationBuilder.CreateTable(
                name: "tour",
                columns: table => new
                {
                    TourId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedTime = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tour__4B16B9E665B63F6D", x => x.TourId);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__users__B9BE370F89EDFDDA", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "pois",
                columns: table => new
                {
                    poi_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    category_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(11,8)", nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: false),
                    radius = table.Column<int>(type: "int", nullable: true, defaultValue: 15),
                    priority = table.Column<int>(type: "int", nullable: true, defaultValue: 1),
                    image_thumbnail = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__pois__6176E7AC68F9D4D2", x => x.poi_id);
                    table.ForeignKey(
                        name: "FK_pois_category",
                        column: x => x.category_id,
                        principalTable: "poi_category",
                        principalColumn: "category_id");
                });

            migrationBuilder.CreateTable(
                name: "user_location_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__user_loc__3213E83F20760A9F", x => x.Id);
                    table.ForeignKey(
                        name: "FK_location_user",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "poi_media",
                columns: table => new
                {
                    media_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    poi_id = table.Column<int>(type: "int", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    media_url = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__poi_medi__D0A840F456B70667", x => x.media_id);
                    table.ForeignKey(
                        name: "FK_media_poi",
                        column: x => x.poi_id,
                        principalTable: "pois",
                        principalColumn: "poi_id");
                });

            migrationBuilder.CreateTable(
                name: "poi_visit_history",
                columns: table => new
                {
                    VisitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    PoiId = table.Column<int>(type: "int", nullable: true),
                    VisitTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ListenDuration = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__poi_visi__375A75E1DEEE7CA0", x => x.VisitId);
                    table.ForeignKey(
                        name: "FK_visit_poi",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "poi_id");
                    table.ForeignKey(
                        name: "FK_visit_user",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "qr_codes",
                columns: table => new
                {
                    QrId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    poi_id = table.Column<int>(type: "int", nullable: false),
                    QrCodeData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__qr_codes__6CD5101BE1633F49", x => x.QrId);
                    table.ForeignKey(
                        name: "FK_qr_poi",
                        column: x => x.poi_id,
                        principalTable: "pois",
                        principalColumn: "poi_id");
                });

            migrationBuilder.CreateTable(
                name: "tour_poi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TourId = table.Column<int>(type: "int", nullable: true),
                    PoiId = table.Column<int>(type: "int", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__tour_poi__3213E83FE13266EB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tourpoi_poi",
                        column: x => x.PoiId,
                        principalTable: "pois",
                        principalColumn: "poi_id");
                    table.ForeignKey(
                        name: "FK_tourpoi_tour",
                        column: x => x.TourId,
                        principalTable: "tour",
                        principalColumn: "TourId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_poi_media_poi_id",
                table: "poi_media",
                column: "poi_id");

            migrationBuilder.CreateIndex(
                name: "IX_poi_visit_history_PoiId",
                table: "poi_visit_history",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_poi_visit_history_UserId",
                table: "poi_visit_history",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_pois_category_id",
                table: "pois",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_qr_codes_poi_id",
                table: "qr_codes",
                column: "poi_id");

            migrationBuilder.CreateIndex(
                name: "IX_tour_poi_PoiId",
                table: "tour_poi",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_tour_poi_TourId",
                table: "tour_poi",
                column: "TourId");

            migrationBuilder.CreateIndex(
                name: "IX_user_location_log_UserId",
                table: "user_location_log",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poi_media");

            migrationBuilder.DropTable(
                name: "poi_visit_history");

            migrationBuilder.DropTable(
                name: "qr_codes");

            migrationBuilder.DropTable(
                name: "tour_poi");

            migrationBuilder.DropTable(
                name: "user_location_log");

            migrationBuilder.DropTable(
                name: "pois");

            migrationBuilder.DropTable(
                name: "tour");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "poi_category");
        }
    }
}
