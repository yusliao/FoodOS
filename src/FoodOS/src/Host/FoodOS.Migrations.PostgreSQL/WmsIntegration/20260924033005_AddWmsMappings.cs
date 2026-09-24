using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.WmsIntegration
{
    /// <inheritdoc />
    public partial class AddWmsMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mappings",
                schema: "wms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConnectionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FoodOsValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ExternalValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FoodOsQuantityPerExternalUnit = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mappings_Provider_ConnectionId_IsActive_Kind",
                schema: "wms",
                table: "Mappings",
                columns: new[] { "Provider", "ConnectionId", "IsActive", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Mappings_Provider_ConnectionId_Kind_ExternalValue",
                schema: "wms",
                table: "Mappings",
                columns: new[] { "Provider", "ConnectionId", "Kind", "ExternalValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mappings_Provider_ConnectionId_Kind_FoodOsValue",
                schema: "wms",
                table: "Mappings",
                columns: new[] { "Provider", "ConnectionId", "Kind", "FoodOsValue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mappings",
                schema: "wms");
        }
    }
}
