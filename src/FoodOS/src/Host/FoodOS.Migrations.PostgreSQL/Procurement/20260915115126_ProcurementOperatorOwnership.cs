using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Procurement
{
    /// <inheritdoc />
    public partial class ProcurementOperatorOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "TraceEvents",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "ReceiveRecords",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "QualityChecks",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "PurchaseOrderLines",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "InboundAppointments",
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
                schema: "procurement",
                table: "TraceEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "Suppliers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "ReceiveRecords",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "QualityChecks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "PurchaseOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "PurchaseOrderLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "procurement",
                table: "InboundAppointments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");
        }
    }
}
