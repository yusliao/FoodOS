using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Warehouse
{
    /// <inheritdoc />
    public partial class WaveZoneRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waves_DailyPlanId_ZoneId",
                schema: "warehouse",
                table: "Waves");

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

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "TenantId" },
                unique: true);
        }
    }
}
