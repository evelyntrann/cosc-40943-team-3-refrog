using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Info.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventLocationsAndCurrentEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReFrogEvents_Status",
                schema: "info",
                table: "ReFrogEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentEventKey",
                schema: "info",
                table: "ReFrogEvents",
                type: "uniqueidentifier",
                nullable: false,
                computedColumnSql: "CASE WHEN [Status] IN ('PLANNED', 'ACTIVE') THEN CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier) ELSE [Id] END",
                stored: true);

            migrationBuilder.CreateTable(
                name: "DonationLocations",
                schema: "info",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    MapPinUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    InfographicUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PodNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationLocations", x => x.Id);
                    table.CheckConstraint("CK_DonationLocation_Status", "[Status] IN ('OPEN', 'CLOSED', 'FULL')");
                    table.ForeignKey(
                        name: "FK_DonationLocations_ReFrogEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "info",
                        principalTable: "ReFrogEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationSchedules",
                schema: "info",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationSchedules", x => x.Id);
                    table.CheckConstraint("CK_LocationSchedule_Day", "[DayOfWeek] IN ('MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY')");
                    table.CheckConstraint("CK_LocationSchedule_Hours", "[CloseTime] > [OpenTime]");
                    table.ForeignKey(
                        name: "FK_LocationSchedules_DonationLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "info",
                        principalTable: "DonationLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReFrogEvents_CurrentEvent",
                schema: "info",
                table: "ReFrogEvents",
                column: "CurrentEventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonationLocations_EventId",
                schema: "info",
                table: "DonationLocations",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationSchedules_LocationId",
                schema: "info",
                table: "LocationSchedules",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationSchedules",
                schema: "info");

            migrationBuilder.DropTable(
                name: "DonationLocations",
                schema: "info");

            migrationBuilder.DropIndex(
                name: "IX_ReFrogEvents_CurrentEvent",
                schema: "info",
                table: "ReFrogEvents");

            migrationBuilder.DropColumn(
                name: "CurrentEventKey",
                schema: "info",
                table: "ReFrogEvents");

            migrationBuilder.CreateIndex(
                name: "IX_ReFrogEvents_Status",
                schema: "info",
                table: "ReFrogEvents",
                column: "Status",
                unique: true,
                filter: "[Status] = 'ACTIVE'");
        }
    }
}
