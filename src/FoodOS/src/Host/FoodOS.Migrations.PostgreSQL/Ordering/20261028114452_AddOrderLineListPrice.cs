using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class AddOrderLineListPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ListUnitPrice",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceSource",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListUnitPrice",
                schema: "ordering",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                schema: "ordering",
                table: "SalesOrderLines");
        }
    }
}
