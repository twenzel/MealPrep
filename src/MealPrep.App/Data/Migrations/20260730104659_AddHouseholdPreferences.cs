using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPrep.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "HouseholdSettings",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AvoidRepeatsWithinDays",
                table: "HouseholdSettings",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<string>(
                name: "DietPreference",
                table: "HouseholdSettings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Alles");

            migrationBuilder.AddColumn<string>(
                name: "ExcludedIngredients",
                table: "HouseholdSettings",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PlannedDinnersPerWeek",
                table: "HouseholdSettings",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "PreferredTags",
                table: "HouseholdSettings",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "schnell, meal prep");

            migrationBuilder.AddColumn<int>(
                name: "WeekendMaxMinutes",
                table: "HouseholdSettings",
                type: "integer",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "AvoidRepeatsWithinDays",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "DietPreference",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "ExcludedIngredients",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "PlannedDinnersPerWeek",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "PreferredTags",
                table: "HouseholdSettings");

            migrationBuilder.DropColumn(
                name: "WeekendMaxMinutes",
                table: "HouseholdSettings");
        }
    }
}
