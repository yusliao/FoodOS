using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Catalog
{
    /// <inheritdoc />
    public partial class AddCustomerLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerLevelId",
                schema: "catalog",
                table: "PriceLists",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerLevels",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false, defaultValue: "root")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLevelAssignments",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false, defaultValue: "root")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLevelAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLevelAssignments_CustomerLevels_CustomerLevelId",
                        column: x => x.CustomerLevelId,
                        principalSchema: "catalog",
                        principalTable: "CustomerLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_CustomerLevelId",
                schema: "catalog",
                table: "PriceLists",
                column: "CustomerLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLevelAssignments_CustomerLevelId",
                schema: "catalog",
                table: "CustomerLevelAssignments",
                column: "CustomerLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLevelAssignments_CustomerOrgId",
                schema: "catalog",
                table: "CustomerLevelAssignments",
                columns: new[] { "CustomerOrgId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLevels_Code",
                schema: "catalog",
                table: "CustomerLevels",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceLists_CustomerLevels_CustomerLevelId",
                schema: "catalog",
                table: "PriceLists",
                column: "CustomerLevelId",
                principalSchema: "catalog",
                principalTable: "CustomerLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceLists_CustomerLevels_CustomerLevelId",
                schema: "catalog",
                table: "PriceLists");

            migrationBuilder.DropTable(
                name: "CustomerLevelAssignments",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "CustomerLevels",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_PriceLists_CustomerLevelId",
                schema: "catalog",
                table: "PriceLists");

            migrationBuilder.DropColumn(
                name: "CustomerLevelId",
                schema: "catalog",
                table: "PriceLists");
        }
    }
}
