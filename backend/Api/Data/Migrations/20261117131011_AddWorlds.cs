using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorlds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceRevision",
                table: "content_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceWorldItemId",
                table: "content_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorldId",
                table: "campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "world_content_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseRevision = table.Column<long>(type: "bigint", nullable: false),
                    PatchJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_content_overrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worlds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    System = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worlds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_world_content_overrides_WorldId_CampaignId_ContentItemId",
                table: "world_content_overrides",
                columns: new[] { "WorldId", "CampaignId", "ContentItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_worlds_OwnerUserId",
                table: "worlds",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_content_overrides");

            migrationBuilder.DropTable(
                name: "worlds");

            migrationBuilder.DropColumn(
                name: "SourceRevision",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "SourceWorldItemId",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "WorldId",
                table: "campaigns");
        }
    }
}
