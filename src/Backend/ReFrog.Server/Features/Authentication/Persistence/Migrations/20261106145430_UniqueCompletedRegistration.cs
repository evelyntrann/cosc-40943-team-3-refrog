using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Authentication.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCompletedRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_AccountId",
                table: "PendingRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_AccountId",
                table: "PendingRegistrations",
                column: "AccountId",
                unique: true,
                filter: "[AccountId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingRegistrations_AccountId",
                table: "PendingRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_AccountId",
                table: "PendingRegistrations",
                column: "AccountId");
        }
    }
}
