using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Warehouse
{
    /// <inheritdoc />
    public partial class WarehouseOperatorOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_RouteId",
                schema: "warehouse",
                table: "Waves");

            migrationBuilder.DropIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_Unrouted",
                schema: "warehouse",
                table: "Waves");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Waves",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "TraceEvents",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "StockPlacements",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Shrinkages",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PutawayTasks",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PickTasks",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PackTotes",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PackToteOrders",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Locations",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_RouteId",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "RouteId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_Unrouted",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_RouteId",
                schema: "warehouse",
                table: "Waves");

            migrationBuilder.DropIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_Unrouted",
                schema: "warehouse",
                table: "Waves");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Waves",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "TraceEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "StockPlacements",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Shrinkages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PutawayTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PickTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PackTotes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "PackToteOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "warehouse",
                table: "Locations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_RouteId",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "RouteId", "TenantId" },
                unique: true,
                filter: "\"RouteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId_Unrouted",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "TenantId" },
                unique: true,
                filter: "\"RouteId\" IS NULL");
        }
    }
}
