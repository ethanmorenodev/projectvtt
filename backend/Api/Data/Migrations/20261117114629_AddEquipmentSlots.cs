using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipmentSlotsJson",
                table: "character_blueprints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentItemId",
                table: "actor_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotId",
                table: "actor_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Volume",
                table: "actor_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Weight",
                table: "actor_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_actor_items_SlotId",
                table: "actor_items",
                column: "SlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_actor_items_SlotId",
                table: "actor_items");

            migrationBuilder.DropColumn(
                name: "EquipmentSlotsJson",
                table: "character_blueprints");

            migrationBuilder.DropColumn(
                name: "ContentItemId",
                table: "actor_items");

            migrationBuilder.DropColumn(
                name: "SlotId",
                table: "actor_items");

            migrationBuilder.DropColumn(
                name: "Volume",
                table: "actor_items");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "actor_items");
        }
    }
}
