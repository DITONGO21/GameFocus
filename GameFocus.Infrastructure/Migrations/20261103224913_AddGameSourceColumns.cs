using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameFocus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutablePath",
                table: "Games",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Games",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Games",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            // Existing rows come from the Steam import (SteamAppId) or were added by hand.
            // Backfill before creating the unique index so existing data cannot violate it.
            migrationBuilder.Sql(@"
UPDATE Games
SET Source = 'Steam', ExternalId = CAST(SteamAppId AS TEXT)
WHERE SteamAppId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Source_ExternalId",
                table: "Games",
                columns: new[] { "Source", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_Source_ExternalId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ExecutablePath",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Games");
        }
    }
}
