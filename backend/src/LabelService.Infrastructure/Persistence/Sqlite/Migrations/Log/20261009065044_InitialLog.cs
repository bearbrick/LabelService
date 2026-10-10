using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelService.Server.Data.LogMigrations
{
    /// <inheritdoc />
    public partial class InitialLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "render_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    request_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    endpoint = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    client_id = table.Column<int>(type: "INTEGER", nullable: true),
                    client_name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    template_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    version_no = table.Column<int>(type: "INTEGER", nullable: true),
                    format = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    layout_code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    item_count = table.Column<int>(type: "INTEGER", nullable: false),
                    duration_ms = table.Column<double>(type: "REAL", nullable: false),
                    stage_ms_json = table.Column<string>(type: "TEXT", nullable: false),
                    is_slow = table.Column<bool>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    error_code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_render_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_render_log_client_id_created_at",
                table: "render_log",
                columns: new[] { "client_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_render_log_created_at",
                table: "render_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_render_log_request_id",
                table: "render_log",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_render_log_status_created_at",
                table: "render_log",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_render_log_template_code_created_at",
                table: "render_log",
                columns: new[] { "template_code", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "render_log");
        }
    }
}
