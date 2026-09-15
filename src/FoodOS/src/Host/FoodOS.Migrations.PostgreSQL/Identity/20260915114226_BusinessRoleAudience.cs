using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Identity
{
    /// <inheritdoc />
    public partial class BusinessRoleAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Audience",
                schema: "identity",
                table: "Roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE identity."Roles"
                SET "Audience" = CASE
                    WHEN LOWER("TenantId") = 'root' THEN 'Operator'
                    ELSE 'Customer'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Audience",
                schema: "identity",
                table: "Roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audience",
                schema: "identity",
                table: "Roles");
        }
    }
}
