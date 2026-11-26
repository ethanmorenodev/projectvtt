using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAssetRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "image_assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "miniatura");

            migrationBuilder.CreateIndex(
                name: "IX_image_assets_Role",
                table: "image_assets",
                column: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_image_assets_Role",
                table: "image_assets");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "image_assets");
        }
    }
}
