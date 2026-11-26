using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackItemKey",
                table: "image_assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackItemKey",
                table: "content_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackKey",
                table: "content_items",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_packs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    System = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    License = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LicenseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttributionJson = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_packs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_image_assets_PackKey_PackItemKey",
                table: "image_assets",
                columns: new[] { "PackKey", "PackItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_items_PackKey_PackItemKey",
                table: "content_items",
                columns: new[] { "PackKey", "PackItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_packs_Key",
                table: "content_packs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_packs");

            migrationBuilder.DropIndex(
                name: "IX_image_assets_PackKey_PackItemKey",
                table: "image_assets");

            migrationBuilder.DropIndex(
                name: "IX_content_items_PackKey_PackItemKey",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "PackItemKey",
                table: "image_assets");

            migrationBuilder.DropColumn(
                name: "PackItemKey",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "PackKey",
                table: "content_items");
        }
    }
}
