using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class OrderDeliveryLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQty",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQty",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VarianceReason",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesOrderLineLots",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShippedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DeliveredQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReturnedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderLineLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderLineLots_SalesOrderLines_SalesOrderLineId",
                        column: x => x.SalesOrderLineId,
                        principalSchema: "ordering",
                        principalTable: "SalesOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLineLots_LotId",
                schema: "ordering",
                table: "SalesOrderLineLots",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLineLots_SalesOrderLineId",
                schema: "ordering",
                table: "SalesOrderLineLots",
                column: "SalesOrderLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesOrderLineLots",
                schema: "ordering");

            migrationBuilder.DropColumn(
                name: "DeliveredQty",
                schema: "ordering",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQty",
                schema: "ordering",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "VarianceReason",
                schema: "ordering",
                table: "SalesOrderLines");
        }
    }
}
