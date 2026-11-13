using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Info.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "info");

            migrationBuilder.CreateTable(
                name: "ReFrogEvents",
                schema: "info",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventYear = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DailyOperatingHoursDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CampusOverview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OverallInfographicUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReFrogEvents", x => x.Id);
                    table.CheckConstraint("CK_ReFrogEvent_Dates", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_ReFrogEvent_Status", "[Status] IN ('PLANNED', 'ACTIVE', 'CLOSED', 'ARCHIVED')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReFrogEvents_Status",
                schema: "info",
                table: "ReFrogEvents",
                column: "Status",
                unique: true,
                filter: "[Status] = 'ACTIVE'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReFrogEvents",
                schema: "info");
        }
    }
}
