using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.WmsIntegration
{
    /// <inheritdoc />
    public partial class AddWmsInventoryProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                schema: "wms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConnectionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalObjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WarehouseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OnHandQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    AllocatedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    QuarantinedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_Provider_ConnectionId_ExternalObjectId",
                schema: "wms",
                table: "InventoryBalances",
                columns: new[] { "Provider", "ConnectionId", "ExternalObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_Provider_ConnectionId_WarehouseId_OwnerId~",
                schema: "wms",
                table: "InventoryBalances",
                columns: new[] { "Provider", "ConnectionId", "WarehouseId", "OwnerId", "Sku", "Uom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryBalances",
                schema: "wms");
        }
    }
}
