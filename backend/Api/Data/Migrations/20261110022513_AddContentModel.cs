using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    System = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "image_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Focal = table.Column<string>(type: "text", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    AlbumsJson = table.Column<string>(type: "text", nullable: false),
                    VariantsJson = table.Column<string>(type: "text", nullable: false),
                    PackKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    License = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttributionJson = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "item_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_usages_actors_ActorId",
                        column: x => x.ActorId,
                        principalTable: "actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    System = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Focal = table.Column<string>(type: "text", nullable: true),
                    PropertiesJson = table.Column<string>(type: "text", nullable: false),
                    InheritanceJson = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    PrivateFieldsJson = table.Column<string>(type: "text", nullable: false),
                    WorkflowState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_items_content_types_TypeId",
                        column: x => x.TypeId,
                        principalTable: "content_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Permission = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_grants_content_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "content_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_relations_content_items_FromItemId",
                        column: x => x.FromItemId,
                        principalTable: "content_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_content_relations_content_items_ToItemId",
                        column: x => x.ToItemId,
                        principalTable: "content_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_revisions_content_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "content_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_grants_ItemId",
                table: "content_grants",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_content_grants_ItemId_SubjectKind_SubjectId_Permission",
                table: "content_grants",
                columns: new[] { "ItemId", "SubjectKind", "SubjectId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_items_Name",
                table: "content_items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_content_items_ParentId",
                table: "content_items",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_items_Scope_CampaignId",
                table: "content_items",
                columns: new[] { "Scope", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_content_items_TypeId",
                table: "content_items",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_content_relations_FromItemId",
                table: "content_relations",
                column: "FromItemId");

            migrationBuilder.CreateIndex(
                name: "IX_content_relations_FromItemId_Kind_ToItemId",
                table: "content_relations",
                columns: new[] { "FromItemId", "Kind", "ToItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_relations_ToItemId",
                table: "content_relations",
                column: "ToItemId");

            migrationBuilder.CreateIndex(
                name: "IX_content_revisions_ItemId_Number",
                table: "content_revisions",
                columns: new[] { "ItemId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_types_System_Key",
                table: "content_types",
                columns: new[] { "System", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_image_assets_OwnerUserId",
                table: "image_assets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_image_assets_PackKey",
                table: "image_assets",
                column: "PackKey");

            migrationBuilder.CreateIndex(
                name: "IX_item_usages_ActorId",
                table: "item_usages",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_item_usages_ItemId",
                table: "item_usages",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_grants");

            migrationBuilder.DropTable(
                name: "content_relations");

            migrationBuilder.DropTable(
                name: "content_revisions");

            migrationBuilder.DropTable(
                name: "image_assets");

            migrationBuilder.DropTable(
                name: "item_usages");

            migrationBuilder.DropTable(
                name: "content_items");

            migrationBuilder.DropTable(
                name: "content_types");
        }
    }
}
