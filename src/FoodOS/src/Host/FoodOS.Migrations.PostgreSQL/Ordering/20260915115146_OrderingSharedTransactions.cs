using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class OrderingSharedTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "Stores",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrders",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "SalesOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrderLineLots",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "ReconcileReminderLogs",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CustomerOrgs",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "Carts",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "Carts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CartLines",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "AfterSalesTickets",
                type: "text",
                nullable: false,
                defaultValue: "root",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "AfterSalesTickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerTenantId_StoreId_Status",
                schema: "ordering",
                table: "SalesOrders",
                columns: new[] { "CustomerTenantId", "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CustomerTenantId_StoreId",
                schema: "ordering",
                table: "Carts",
                columns: new[] { "CustomerTenantId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_AfterSalesTickets_CustomerTenantId_StoreId",
                schema: "ordering",
                table: "AfterSalesTickets",
                columns: new[] { "CustomerTenantId", "StoreId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_CustomerTenantId_StoreId_Status",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_Carts_CustomerTenantId_StoreId",
                schema: "ordering",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_AfterSalesTickets_CustomerTenantId_StoreId",
                schema: "ordering",
                table: "AfterSalesTickets");

            migrationBuilder.DropColumn(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "AfterSalesTickets");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "Stores",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrderLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "SalesOrderLineLots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "ReconcileReminderLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CustomerOrgs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "Carts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "CartLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "ordering",
                table: "AfterSalesTickets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "root");
        }
    }
}
