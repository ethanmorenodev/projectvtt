using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActorsAndTokenActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "actors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RulesetKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttributesJson = table.Column<string>(type: "text", nullable: false),
                    ResourcesJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_actors_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tokens_ActorId",
                table: "tokens",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_actors_CampaignId",
                table: "actors",
                column: "CampaignId");

            migrationBuilder.AddForeignKey(
                name: "FK_tokens_actors_ActorId",
                table: "tokens",
                column: "ActorId",
                principalTable: "actors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tokens_actors_ActorId",
                table: "tokens");

            migrationBuilder.DropTable(
                name: "actors");

            migrationBuilder.DropIndex(
                name: "IX_tokens_ActorId",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "tokens");
        }
    }
}
