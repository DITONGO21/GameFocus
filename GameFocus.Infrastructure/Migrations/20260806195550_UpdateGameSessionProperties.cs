using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameFocus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGameSessionProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "Sessions",
                newName: "StartTimeUtc");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "Sessions",
                newName: "EndTimeUtc");

            migrationBuilder.RenameColumn(
                name: "DurationMinutes",
                table: "Sessions",
                newName: "DurationSeconds");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "StartTimeUtc",
                table: "Sessions",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "EndTimeUtc",
                table: "Sessions",
                newName: "EndTime");

            migrationBuilder.RenameColumn(
                name: "DurationSeconds",
                table: "Sessions",
                newName: "DurationMinutes");
        }
    }
}
