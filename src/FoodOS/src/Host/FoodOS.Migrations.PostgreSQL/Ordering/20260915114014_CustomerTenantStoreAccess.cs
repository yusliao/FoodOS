using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class CustomerTenantStoreAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "Stores",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "CustomerOrgs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerUserStoreAccesses",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerTenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerUserStoreAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerUserStoreAccesses_CustomerOrgs_CustomerOrgId",
                        column: x => x.CustomerOrgId,
                        principalSchema: "ordering",
                        principalTable: "CustomerOrgs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerUserStoreAccesses_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "ordering",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CustomerTenantId_CustomerOrgId",
                schema: "ordering",
                table: "Stores",
                columns: new[] { "CustomerTenantId", "CustomerOrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrgs_CustomerTenantId",
                schema: "ordering",
                table: "CustomerOrgs",
                columns: new[] { "CustomerTenantId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUserStoreAccesses_CustomerOrgId",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                column: "CustomerOrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUserStoreAccesses_CustomerTenantId_CustomerOrgId_Is~",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                columns: new[] { "CustomerTenantId", "CustomerOrgId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUserStoreAccesses_CustomerTenantId_UserId_StoreId",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                columns: new[] { "CustomerTenantId", "UserId", "StoreId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUserStoreAccesses_StoreId",
                schema: "ordering",
                table: "CustomerUserStoreAccesses",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerUserStoreAccesses",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CustomerTenantId_CustomerOrgId",
                schema: "ordering",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_CustomerOrgs_CustomerTenantId",
                schema: "ordering",
                table: "CustomerOrgs");

            migrationBuilder.DropColumn(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CustomerTenantId",
                schema: "ordering",
                table: "CustomerOrgs");
        }
    }
}
