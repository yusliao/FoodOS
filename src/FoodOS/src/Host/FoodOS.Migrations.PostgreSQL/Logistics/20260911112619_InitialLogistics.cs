using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodOS.Migrations.PostgreSQL.Logistics
{
    /// <inheritdoc />
    public partial class InitialLogistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "logistics");

            migrationBuilder.CreateTable(
                name: "Drivers",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StoreSequence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DefaultVehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemperatureReadings",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Compartment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Celsius = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemperatureReadings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraceEvents",
                schema: "logistics",
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
                name: "Vehicles",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CompartmentZones = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturnsOnTruck",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnsOnTruck", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnsOnTruck_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "logistics",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLines",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "logistics",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentStops",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Window = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentStops_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "logistics",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLineLots",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Zone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLineLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentLineLots_ShipmentLines_ShipmentLineId",
                        column: x => x.ShipmentLineId,
                        principalSchema: "logistics",
                        principalTable: "ShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProofOfDeliveries",
                schema: "logistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedQtyJson = table.Column<string>(type: "text", nullable: false),
                    PhotoFileIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Geo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProofOfDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProofOfDeliveries_ShipmentStops_StopId",
                        column: x => x.StopId,
                        principalSchema: "logistics",
                        principalTable: "ShipmentStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_UserId",
                schema: "logistics",
                table: "Drivers",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProofOfDeliveries_StopId",
                schema: "logistics",
                table: "ProofOfDeliveries",
                columns: new[] { "StopId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProofOfDeliveries_StopId1",
                schema: "logistics",
                table: "ProofOfDeliveries",
                column: "StopId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnsOnTruck_OrderId",
                schema: "logistics",
                table: "ReturnsOnTruck",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnsOnTruck_ShipmentId",
                schema: "logistics",
                table: "ReturnsOnTruck",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_WarehouseId_Code",
                schema: "logistics",
                table: "Routes",
                columns: new[] { "WarehouseId", "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLineLots_ShipmentLineId",
                schema: "logistics",
                table: "ShipmentLineLots",
                column: "ShipmentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_OrderId",
                schema: "logistics",
                table: "ShipmentLines",
                columns: new[] { "OrderId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_ShipmentId",
                schema: "logistics",
                table: "ShipmentLines",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Number",
                schema: "logistics",
                table: "Shipments",
                columns: new[] { "Number", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_RouteId_BusinessDate",
                schema: "logistics",
                table: "Shipments",
                columns: new[] { "RouteId", "BusinessDate", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentStops_ShipmentId_Sequence",
                schema: "logistics",
                table: "ShipmentStops",
                columns: new[] { "ShipmentId", "Sequence", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemperatureReadings_VehicleId_RecordedAt",
                schema: "logistics",
                table: "TemperatureReadings",
                columns: new[] { "VehicleId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_LotId",
                schema: "logistics",
                table: "TraceEvents",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceEvents_OccurredAt",
                schema: "logistics",
                table: "TraceEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Plate",
                schema: "logistics",
                table: "Vehicles",
                columns: new[] { "Plate", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drivers",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "ProofOfDeliveries",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "ReturnsOnTruck",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "Routes",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "ShipmentLineLots",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "TemperatureReadings",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "TraceEvents",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "Vehicles",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "ShipmentStops",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "ShipmentLines",
                schema: "logistics");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "logistics");
        }
    }
}
