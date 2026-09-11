using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Warehouse
{
    /// <inheritdoc />
    public partial class InitialWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warehouse");

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraceEvents",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BizStep = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Disposition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceLocation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DestLocation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraceEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Waves",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    DailyPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Zone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickTasks",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Zone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    LotNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ShortageQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PickerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickTasks_Waves_WaveId",
                        column: x => x.WaveId,
                        principalSchema: "warehouse",
                        principalTable: "Waves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_WarehouseId_Code",
                schema: "warehouse",
                table: "Locations",
                columns: new[] { "WarehouseId", "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickTasks_OrderId",
                schema: "warehouse",
                table: "PickTasks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PickTasks_WaveId",
                schema: "warehouse",
                table: "PickTasks",
                column: "WaveId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_LotId",
                schema: "warehouse",
                table: "TraceEvents",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_OccurredAt",
                schema: "warehouse",
                table: "TraceEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_DailyPlanId_ZoneId",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "DailyPlanId", "ZoneId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waves_Number",
                schema: "warehouse",
                table: "Waves",
                columns: new[] { "Number", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Locations",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "PickTasks",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "TraceEvents",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "Waves",
                schema: "warehouse");
        }
    }
}
