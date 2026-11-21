using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Volunteer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVolunteer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vol");

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                schema: "vol",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.CheckConstraint("CK_ShiftAssignment_Status", "[Status] IN ('ACTIVE', 'PENDING', 'CANCELLED', 'DENIED')");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ShiftAssignment_Shift_Volunteer",
                schema: "vol",
                table: "ShiftAssignments",
                columns: new[] { "ShiftId", "VolunteerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId_Status",
                schema: "vol",
                table: "ShiftAssignments",
                columns: new[] { "ShiftId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_VolunteerId_Status",
                schema: "vol",
                table: "ShiftAssignments",
                columns: new[] { "VolunteerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftAssignments",
                schema: "vol");
        }
    }
}
