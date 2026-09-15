using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Catalog
{
    /// <inheritdoc />
    public partial class CatalogOperatorOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                schema: "catalog",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_Slug",
                schema: "catalog",
                table: "Brands");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductTranslations",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductImages",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductContractLocks",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "PriceLists",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "PriceListLines",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Categories",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Brands",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                schema: "catalog",
                table: "Products",
                columns: new[] { "Sku", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                schema: "catalog",
                table: "Products",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                schema: "catalog",
                table: "Brands",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                schema: "catalog",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_Slug",
                schema: "catalog",
                table: "Brands");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductTranslations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductImages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "ProductContractLocks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "PriceLists",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "PriceListLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "catalog",
                table: "Brands",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                schema: "catalog",
                table: "Products",
                columns: new[] { "Sku", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                schema: "catalog",
                table: "Products",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                schema: "catalog",
                table: "Categories",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                schema: "catalog",
                table: "Brands",
                columns: new[] { "Slug", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
