using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LabelService.Infrastructure.Persistence.PostgreSql.Migrations.Log
{
    /// <inheritdoc />
    public partial class InitialLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "op_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    detail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_op_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "render_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    client_id = table.Column<int>(type: "integer", nullable: true),
                    client_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    template_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: true),
                    format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    layout_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<double>(type: "double precision", nullable: false),
                    stage_ms_json = table.Column<string>(type: "text", nullable: false),
                    is_slow = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    error_message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_render_log", x => x.id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_render_log_trace_id",
                table: "render_log",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "op_log");

            migrationBuilder.DropTable(
                name: "render_log");
        }
    }
}
