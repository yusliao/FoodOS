using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Catalog
{
    /// <inheritdoc />
    public partial class CatalogPriceLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceLists",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerOrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductContractLocks",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductContractLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceListLines",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListLines_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "catalog",
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLines_PriceListId_ProductId_MinQty",
                schema: "catalog",
                table: "PriceListLines",
                columns: new[] { "PriceListId", "ProductId", "MinQty", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_CustomerOrgId",
                schema: "catalog",
                table: "PriceLists",
                column: "CustomerOrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_ValidFrom_ValidTo",
                schema: "catalog",
                table: "PriceLists",
                columns: new[] { "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductContractLocks_CustomerOrgId_ProductId",
                schema: "catalog",
                table: "ProductContractLocks",
                columns: new[] { "CustomerOrgId", "ProductId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductContractLocks_Until",
                schema: "catalog",
                table: "ProductContractLocks",
                column: "Until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceListLines",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductContractLocks",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "PriceLists",
                schema: "catalog");
        }
    }
}
