using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPrep.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedLunches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlannedLunchesPerWeek",
                table: "HouseholdSettings",
                type: "integer",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedLunchesPerWeek",
                table: "HouseholdSettings");
        }
    }
}
