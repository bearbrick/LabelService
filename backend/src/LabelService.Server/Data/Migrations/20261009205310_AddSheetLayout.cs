using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sheet_layout",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    paper_width_mm = table.Column<double>(type: "REAL", nullable: false),
                    paper_height_mm = table.Column<double>(type: "REAL", nullable: false),
                    slot_width_mm = table.Column<double>(type: "REAL", nullable: false),
                    slot_height_mm = table.Column<double>(type: "REAL", nullable: false),
                    slots_json = table.Column<string>(type: "TEXT", nullable: false),
                    offset_x_mm = table.Column<double>(type: "REAL", nullable: false),
                    offset_y_mm = table.Column<double>(type: "REAL", nullable: false),
                    cut_lines = table.Column<bool>(type: "INTEGER", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sheet_layout", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sheet_layout_code",
                table: "sheet_layout",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sheet_layout");
        }
    }
}
