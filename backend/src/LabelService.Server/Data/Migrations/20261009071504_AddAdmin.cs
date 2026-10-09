using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabelService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                table: "label_template");

            migrationBuilder.DropColumn(
                name: "allowed_templates",
                table: "api_client");

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "label_template_version",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "business_id",
                table: "label_template",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "label_template",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "canvas_preset",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "key",
                table: "asset",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "all_businesses",
                table: "api_client",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "app_id",
                table: "api_client",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "business_ids",
                table: "api_client",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "api_client",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ip_allowlist",
                table: "api_client",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "app_role",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    all_permissions = table.Column<bool>(type: "INTEGER", nullable: false),
                    permissions = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    builtin = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "business",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    owner_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    account = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    must_change_password = table.Column<bool>(type: "INTEGER", nullable: false),
                    role_id = table.Column<int>(type: "INTEGER", nullable: false),
                    all_businesses = table.Column<bool>(type: "INTEGER", nullable: false),
                    business_ids = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    failed_logins = table.Column<int>(type: "INTEGER", nullable: false),
                    locked_until = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 1,
                column: "kind",
                value: "办公纸张");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 2,
                column: "kind",
                value: "办公纸张");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 3,
                column: "kind",
                value: "标签纸");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 4,
                column: "kind",
                value: "标签纸");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 5,
                column: "kind",
                value: "标签纸");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 6,
                column: "kind",
                value: "标签纸");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 7,
                column: "kind",
                value: "标签纸");

            migrationBuilder.UpdateData(
                table: "canvas_preset",
                keyColumn: "id",
                keyValue: 8,
                column: "kind",
                value: "标签纸");

            migrationBuilder.CreateIndex(
                name: "IX_label_template_business_id",
                table: "label_template",
                column: "business_id");

            migrationBuilder.CreateIndex(
                name: "IX_asset_key",
                table: "asset",
                column: "key",
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
                name: "IX_business_code",
                table: "business",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_label_template_business_business_id",
                table: "label_template",
                column: "business_id",
                principalTable: "business",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_label_template_business_business_id",
                table: "label_template");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "business");

            migrationBuilder.DropTable(
                name: "app_role");

            migrationBuilder.DropIndex(
                name: "IX_label_template_business_id",
                table: "label_template");

            migrationBuilder.DropIndex(
                name: "IX_asset_key",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "note",
                table: "label_template_version");

            migrationBuilder.DropColumn(
                name: "business_id",
                table: "label_template");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "label_template");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "canvas_preset");

            migrationBuilder.DropColumn(
                name: "key",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "all_businesses",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "app_id",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "business_ids",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "description",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "ip_allowlist",
                table: "api_client");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "label_template",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "allowed_templates",
                table: "api_client",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }
    }
}
