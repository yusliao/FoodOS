using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Ordering
{
    /// <inheritdoc />
    public partial class AddOrderDeskAndStoreContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlacedByUserId",
                schema: "ordering",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacementChannel",
                schema: "ordering",
                table: "SalesOrders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoreContacts",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PhoneDigits = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false, defaultValue: "root")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreContacts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "ordering",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreContacts_PhoneDigits",
                schema: "ordering",
                table: "StoreContacts",
                column: "PhoneDigits");

            migrationBuilder.CreateIndex(
                name: "IX_StoreContacts_StoreId_PhoneDigits",
                schema: "ordering",
                table: "StoreContacts",
                columns: new[] { "StoreId", "PhoneDigits", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreContacts",
                schema: "ordering");

            migrationBuilder.DropColumn(
                name: "PlacedByUserId",
                schema: "ordering",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "PlacementChannel",
                schema: "ordering",
                table: "SalesOrders");
        }
    }
}
