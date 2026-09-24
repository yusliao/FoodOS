using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class AddWarehouseOrderConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlacementIdempotencyKey",
                schema: "ordering",
                table: "SalesOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseConfirmationDetail",
                schema: "ordering",
                table: "SalesOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseConfirmationStatus",
                schema: "ordering",
                table: "SalesOrders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NotTracked");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarehouseConfirmationUpdatedAt",
                schema: "ordering",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerTenantId_PlacementIdempotencyKey",
                schema: "ordering",
                table: "SalesOrders",
                columns: new[] { "CustomerTenantId", "PlacementIdempotencyKey", "TenantId" },
                unique: true,
                filter: "\"PlacementIdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_CustomerTenantId_PlacementIdempotencyKey",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "PlacementIdempotencyKey",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseConfirmationDetail",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseConfirmationStatus",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseConfirmationUpdatedAt",
                schema: "ordering",
                table: "SalesOrders");
        }
    }
}
