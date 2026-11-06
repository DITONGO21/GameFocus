using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameFocus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Games.ExecutableName is already created by InitialCreate, so this
            // migration is a no-op. Kept so the migration history stays consistent.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Column is owned by InitialCreate; dropping it here would be wrong.
        }
    }
}
