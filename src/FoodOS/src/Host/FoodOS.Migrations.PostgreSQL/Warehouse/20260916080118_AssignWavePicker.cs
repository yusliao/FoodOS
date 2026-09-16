using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Warehouse
{
    /// <inheritdoc />
    public partial class AssignWavePicker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedPickerUserId",
                schema: "warehouse",
                table: "Waves",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waves_AssignedPickerUserId",
                schema: "warehouse",
                table: "Waves",
                column: "AssignedPickerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waves_AssignedPickerUserId",
                schema: "warehouse",
                table: "Waves");

            migrationBuilder.DropColumn(
                name: "AssignedPickerUserId",
                schema: "warehouse",
                table: "Waves");
        }
    }
}
