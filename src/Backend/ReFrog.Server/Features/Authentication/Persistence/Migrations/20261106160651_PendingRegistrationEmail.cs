using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReFrog.Server.Features.Authentication.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingRegistrationEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "PendingRegistrations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Registrations started before this column existed only kept the normalized address.
            migrationBuilder.Sql("UPDATE [PendingRegistrations] SET [Email] = [NormalizedEmail] WHERE [Email] = N''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "PendingRegistrations");
        }
    }
}
