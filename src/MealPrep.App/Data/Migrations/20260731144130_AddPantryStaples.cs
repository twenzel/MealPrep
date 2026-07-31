using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPrep.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryStaples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PantryStaples",
                table: "HouseholdSettings",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "Salz, Pfeffer, Öl, Wasser, Gewürze");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PantryStaples",
                table: "HouseholdSettings");
        }
    }
}
