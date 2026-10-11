using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelService.Infrastructure.Persistence.Sqlite.Migrations.Main
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_setting",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_setting", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_setting");
        }
    }
}
