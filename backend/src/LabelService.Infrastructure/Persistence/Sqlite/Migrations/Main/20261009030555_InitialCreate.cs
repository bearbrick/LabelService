using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LabelService.Infrastructure.Persistence.Sqlite.Migrations.Main
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    size = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "canvas_preset",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    width_mm = table.Column<double>(type: "REAL", nullable: false),
                    height_mm = table.Column<double>(type: "REAL", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canvas_preset", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "label_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    latest_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "label_template_version",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    template_id = table.Column<int>(type: "INTEGER", nullable: false),
                    version_no = table.Column<int>(type: "INTEGER", nullable: false),
                    content_json = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    published_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    published_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_template_version", x => x.id);
                    table.ForeignKey(
                        name: "FK_label_template_version_label_template_template_id",
                        column: x => x.template_id,
                        principalTable: "label_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "canvas_preset",
                columns: new[] { "id", "enabled", "height_mm", "name", "sort", "width_mm" },
                values: new object[,]
                {
                    { 1, true, 297.0, "A4", 10, 210.0 },
                    { 2, true, 210.0, "A5", 20, 148.0 },
                    { 3, true, 150.0, "100×150", 30, 100.0 },
                    { 4, true, 100.0, "100×100", 40, 100.0 },
                    { 5, true, 60.0, "100×60", 50, 100.0 },
                    { 6, true, 50.0, "80×50", 60, 80.0 },
                    { 7, true, 40.0, "60×40", 70, 60.0 },
                    { 8, true, 30.0, "50×30", 80, 50.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_sha256",
                table: "asset",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "IX_label_template_code",
                table: "label_template",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_label_template_version_template_id_version_no",
                table: "label_template_version",
                columns: new[] { "template_id", "version_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset");

            migrationBuilder.DropTable(
                name: "canvas_preset");

            migrationBuilder.DropTable(
                name: "label_template_version");

            migrationBuilder.DropTable(
                name: "label_template");
        }
    }
}
