using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameFocus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalTargetLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetLabel",
                table: "Goals",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetLabel",
                table: "Goals");
        }
    }
}
