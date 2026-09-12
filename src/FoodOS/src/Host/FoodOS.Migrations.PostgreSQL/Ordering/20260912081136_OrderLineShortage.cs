using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class OrderLineShortage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShortageQty",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShortageReason",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortageQty",
                schema: "ordering",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ShortageReason",
                schema: "ordering",
                table: "SalesOrderLines");
        }
    }
}
