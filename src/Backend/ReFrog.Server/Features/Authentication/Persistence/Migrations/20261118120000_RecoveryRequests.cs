using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Authentication.Persistence.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20261118120000_RecoveryRequests")]
public sealed class RecoveryRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecoveryRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecoveryRequests", item => item.Id);
                table.ForeignKey(
                    name: "FK_RecoveryRequests_AspNetUsers_AccountId",
                    column: item => item.AccountId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecoveryRequests_AccountId",
            table: "RecoveryRequests",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_RecoveryRequests_NormalizedEmail_AcceptedAt",
            table: "RecoveryRequests",
            columns: new[] { "NormalizedEmail", "AcceptedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "RecoveryRequests");
}
