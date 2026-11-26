using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneCombats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "combatants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SceneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Initiative = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_combatants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_combatants_scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_combatants_tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scene_combats",
                columns: table => new
                {
                    SceneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    CurrentCombatantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scene_combats", x => x.SceneId);
                    table.ForeignKey(
                        name: "FK_scene_combats_combatants_CurrentCombatantId",
                        column: x => x.CurrentCombatantId,
                        principalTable: "combatants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scene_combats_scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_combatants_SceneId_Initiative_CreatedAt",
                table: "combatants",
                columns: new[] { "SceneId", "Initiative", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_combatants_TokenId",
                table: "combatants",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_scene_combats_CurrentCombatantId",
                table: "scene_combats",
                column: "CurrentCombatantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scene_combats");

            migrationBuilder.DropTable(
                name: "combatants");
        }
    }
}
