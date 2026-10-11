using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelService.Infrastructure.Persistence.Sqlite.Migrations.Log
{
    /// <inheritdoc />
    public partial class AddOpLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "render_log",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip",
                table: "render_log",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                table: "render_log",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "op_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    target = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    detail = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_op_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_render_log_trace_id",
                table: "render_log",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "IX_op_log_created_at",
                table: "op_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_op_log_type_created_at",
                table: "op_log",
                columns: new[] { "type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_op_log_user_id_created_at",
                table: "op_log",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_log");

            migrationBuilder.DropIndex(
                name: "IX_render_log_trace_id",
                table: "render_log");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "render_log");

            migrationBuilder.DropColumn(
                name: "ip",
                table: "render_log");

            migrationBuilder.DropColumn(
                name: "trace_id",
                table: "render_log");
        }
    }
}
