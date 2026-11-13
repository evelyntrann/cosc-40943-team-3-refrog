using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Authentication.Persistence.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20261113160000_AdministratorSignInAudit")]
public sealed class AdministratorSignInAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdministratorSignInAudits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Succeeded = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_AdministratorSignInAudits", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AdministratorSignInAudits_AccountId_OccurredAt",
            table: "AdministratorSignInAudits",
            columns: new[] { "AccountId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AdministratorSignInAudits");
}
