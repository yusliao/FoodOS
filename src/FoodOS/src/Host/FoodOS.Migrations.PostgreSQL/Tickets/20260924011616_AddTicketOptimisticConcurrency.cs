using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Tickets
{
    /// <inheritdoc />
    public partial class AddTicketOptimisticConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL supplies xmin as a system column for every row. The model snapshot
            // records it as the ticket concurrency token; no physical schema change is needed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin is owned by PostgreSQL and must not be dropped.
        }
    }
}
