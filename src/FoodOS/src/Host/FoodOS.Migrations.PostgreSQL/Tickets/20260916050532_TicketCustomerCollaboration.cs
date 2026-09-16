using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Tickets
{
    /// <inheritdoc />
    public partial class TicketCustomerCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TenantId_ReporterUserId",
                schema: "tickets",
                table: "Tickets",
                columns: new[] { "TenantId", "ReporterUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_TenantId_ReporterUserId",
                schema: "tickets",
                table: "Tickets");
        }
    }
}
