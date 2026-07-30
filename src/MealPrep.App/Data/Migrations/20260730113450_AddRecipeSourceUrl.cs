using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPrep.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Recipes",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Recipes");
        }
    }
}
