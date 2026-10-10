using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LabelService.Infrastructure.Persistence.PostgreSql.Migrations.Main
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_client",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    app_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    all_businesses = table.Column<bool>(type: "boolean", nullable: false),
                    business_ids = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ip_allowlist = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_role",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    all_permissions = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    builtin = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "business",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    owner_user_id = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "canvas_preset",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    width_mm = table.Column<double>(type: "double precision", nullable: false),
                    height_mm = table.Column<double>(type: "double precision", nullable: false),
                    sort = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canvas_preset", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sheet_layout",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    paper_width_mm = table.Column<double>(type: "double precision", nullable: false),
                    paper_height_mm = table.Column<double>(type: "double precision", nullable: false),
                    slot_width_mm = table.Column<double>(type: "double precision", nullable: false),
                    slot_height_mm = table.Column<double>(type: "double precision", nullable: false),
                    slots_json = table.Column<string>(type: "text", nullable: false),
                    offset_x_mm = table.Column<double>(type: "double precision", nullable: false),
                    offset_y_mm = table.Column<double>(type: "double precision", nullable: false),
                    cut_lines = table.Column<bool>(type: "boolean", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sheet_layout", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    all_businesses = table.Column<bool>(type: "boolean", nullable: false),
                    business_ids = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    failed_logins = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_user_app_role_role_id",
                        column: x => x.role_id,
                        principalTable: "app_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "label_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    business_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    latest_version = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_template", x => x.id);
                    table.ForeignKey(
                        name: "FK_label_template_business_business_id",
                        column: x => x.business_id,
                        principalTable: "business",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "label_template_version",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    content_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    published_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: false)
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
                columns: new[] { "id", "enabled", "height_mm", "kind", "name", "sort", "width_mm" },
                values: new object[,]
                {
                    { 1, true, 297.0, "办公纸张", "A4", 10, 210.0 },
                    { 2, true, 210.0, "办公纸张", "A5", 20, 148.0 },
                    { 3, true, 150.0, "标签纸", "100×150", 30, 100.0 },
                    { 4, true, 100.0, "标签纸", "100×100", 40, 100.0 },
                    { 5, true, 60.0, "标签纸", "100×60", 50, 100.0 },
                    { 6, true, 50.0, "标签纸", "80×50", 60, 80.0 },
                    { 7, true, 40.0, "标签纸", "60×40", 70, 60.0 },
                    { 8, true, 30.0, "标签纸", "50×30", 80, 50.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_client_api_key_hash",
                table: "api_client",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_client_name",
                table: "api_client",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_role_name",
                table: "app_role",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_account",
                table: "app_user",
                column: "account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_role_id",
                table: "app_user",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_asset_key",
                table: "asset",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_sha256",
                table: "asset",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "IX_business_code",
                table: "business",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_label_template_business_id",
                table: "label_template",
                column: "business_id");

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
                name: "api_client");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "asset");

            migrationBuilder.DropTable(
                name: "canvas_preset");

            migrationBuilder.DropTable(
                name: "label_template_version");

            migrationBuilder.DropTable(
                name: "sheet_layout");

            migrationBuilder.DropTable(
                name: "app_role");

            migrationBuilder.DropTable(
                name: "label_template");

            migrationBuilder.DropTable(
                name: "business");
        }
    }
}
