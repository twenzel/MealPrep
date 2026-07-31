using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.App.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingItemState> ShoppingItemStates => Set<ShoppingItemState>();
    public DbSet<HouseholdSettings> HouseholdSettings => Set<HouseholdSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Recipe>(entity =>
        {
            entity.Property(recipe => recipe.Name).HasMaxLength(160);
            entity.Property(recipe => recipe.Category).HasMaxLength(80);
            entity.Property(recipe => recipe.Tags).HasMaxLength(400);
            entity.Property(recipe => recipe.ImageContentType).HasMaxLength(80);
            entity.Property(recipe => recipe.SourceUrl).HasMaxLength(600);
            entity.Property(recipe => recipe.AccentColor).HasMaxLength(16);
            entity.Property(recipe => recipe.Emoji).HasMaxLength(16);
        });

        builder.Entity<RecipeIngredient>(entity =>
        {
            entity.Property(ingredient => ingredient.Quantity).HasPrecision(10, 2);
            entity.Property(ingredient => ingredient.Name).HasMaxLength(160);
            entity.Property(ingredient => ingredient.Unit).HasMaxLength(30);
            entity.Property(ingredient => ingredient.Aisle).HasMaxLength(80);
        });

        builder.Entity<MealPlanEntry>()
            .HasIndex(entry => new { entry.Date, entry.MealType })
            .IsUnique();

        builder.Entity<ShoppingItemState>()
            .HasIndex(state => new { state.WeekStart, state.ItemKey })
            .IsUnique();

        builder.Entity<HouseholdSettings>(entity =>
        {
            entity.Property(settings => settings.HouseholdName).HasMaxLength(120);
            entity.Property(settings => settings.DietPreference).HasMaxLength(40);
            entity.Property(settings => settings.PreferredTags).HasMaxLength(600);
            entity.Property(settings => settings.Allergies).HasMaxLength(800);
            entity.Property(settings => settings.ExcludedIngredients).HasMaxLength(800);
            entity.Property(settings => settings.PantryStaples).HasMaxLength(800);
        });
    }
}
