using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Inventory
{
    /// <inheritdoc />
    public partial class InventoryOperatorOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Warehouses",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "TemperatureZones",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Lots",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "LotBalances",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "InventoryTransactions",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "DailyPlans",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Warehouses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "TemperatureZones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Reservations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "Lots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "LotBalances",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "InventoryTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "inventory",
                table: "DailyPlans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");
        }
    }
}
