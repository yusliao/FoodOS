using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.WmsIntegration
{
    /// <inheritdoc />
    public partial class ScopeWmsOutboundOperationIdempotencyByTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboundOperations_Provider_ConnectionId_IdempotencyKey",
                schema: "wms",
                table: "OutboundOperations");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOperations_TenantId_Provider_ConnectionId_Idempoten~",
                schema: "wms",
                table: "OutboundOperations",
                columns: new[] { "TenantId", "Provider", "ConnectionId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboundOperations_TenantId_Provider_ConnectionId_Idempoten~",
                schema: "wms",
                table: "OutboundOperations");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundOperations_Provider_ConnectionId_IdempotencyKey",
                schema: "wms",
                table: "OutboundOperations",
                columns: new[] { "Provider", "ConnectionId", "IdempotencyKey" },
                unique: true);
        }
    }
}
