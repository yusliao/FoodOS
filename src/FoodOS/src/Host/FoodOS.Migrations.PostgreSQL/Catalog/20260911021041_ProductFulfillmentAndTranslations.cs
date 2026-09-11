using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Catalog
{
    /// <inheritdoc />
    public partial class ProductFulfillmentAndTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                schema: "catalog",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUom",
                schema: "catalog",
                table: "Products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "EA");

            migrationBuilder.AddColumn<bool>(
                name: "CatchWeight",
                schema: "catalog",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinRemainingDaysOnShip",
                schema: "catalog",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                schema: "catalog",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageNote",
                schema: "catalog",
                table: "Products",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemperatureZone",
                schema: "catalog",
                table: "Products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Ambient");

            migrationBuilder.CreateTable(
                name: "ProductTranslations",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Culture = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductTranslations_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: "catalog",
                table: "Products",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTranslations_ProductId_Culture",
                schema: "catalog",
                table: "ProductTranslations",
                columns: new[] { "ProductId", "Culture", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductTranslations",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Barcode",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BaseUom",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CatchWeight",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinRemainingDaysOnShip",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageNote",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TemperatureZone",
                schema: "catalog",
                table: "Products");
        }
    }
}
