using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Warehouse
{
    /// <inheritdoc />
    public partial class PutawayPackShrink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackToteOrders",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackToteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackToteOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackTotes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sscc = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    DockLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackTotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PutawayTasks",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Zone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    SuggestedLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shrinkages",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Zone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PhotoFileIds = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shrinkages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockPlacements",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockPlacements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackToteOrders_OrderId",
                schema: "warehouse",
                table: "PackToteOrders",
                columns: new[] { "OrderId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackToteOrders_PackToteId_OrderId",
                schema: "warehouse",
                table: "PackToteOrders",
                columns: new[] { "PackToteId", "OrderId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackTotes_Sscc",
                schema: "warehouse",
                table: "PackTotes",
                columns: new[] { "Sscc", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PutawayTasks_LotId_Status",
                schema: "warehouse",
                table: "PutawayTasks",
                columns: new[] { "LotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Shrinkages_LotId",
                schema: "warehouse",
                table: "Shrinkages",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockPlacements_LotId_LocationId",
                schema: "warehouse",
                table: "StockPlacements",
                columns: new[] { "LotId", "LocationId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackToteOrders",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "PackTotes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "PutawayTasks",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "Shrinkages",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "StockPlacements",
                schema: "warehouse");
        }
    }
}
