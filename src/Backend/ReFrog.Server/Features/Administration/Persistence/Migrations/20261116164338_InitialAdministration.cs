using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Administration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "adm");

            migrationBuilder.CreateTable(
                name: "AdminAuditEntries",
                schema: "adm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationStaffing",
                schema: "adm",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultRequiredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationStaffing", x => new { x.EventId, x.LocationId });
                    table.CheckConstraint("CK_LocationStaffing_DefaultRequiredCount", "[DefaultRequiredCount] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "adm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    CreateRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shift_RequiredCount", "[RequiredCount] > 0");
                    table.CheckConstraint("CK_Shift_Times", "[EndsAt] > [StartsAt]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_ActorAccountId_OccurredAt",
                schema: "adm",
                table: "AdminAuditEntries",
                columns: new[] { "ActorAccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CreateRequestId",
                schema: "adm",
                table: "Shifts",
                column: "CreateRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_EventId_StartsAt",
                schema: "adm",
                table: "Shifts",
                columns: new[] { "EventId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditEntries",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "LocationStaffing",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "adm");
        }
    }
}
