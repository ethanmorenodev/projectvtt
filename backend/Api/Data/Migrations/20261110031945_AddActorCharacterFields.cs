using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActorCharacterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Biography",
                table: "actors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildJson",
                table: "actors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "actors",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "actors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Personality",
                table: "actors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PortraitId",
                table: "actors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemDataJson",
                table: "actors",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Biography",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "BuildJson",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "Personality",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "PortraitId",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "SystemDataJson",
                table: "actors");
        }
    }
}
