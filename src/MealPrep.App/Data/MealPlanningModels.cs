using System.ComponentModel.DataAnnotations;

namespace MealPrep.App.Data;

public static class MealTypes
{
    public const string Lunch = "Mittagessen";
    public const string Dinner = "Abendessen";

    public static readonly IReadOnlyList<string> All = [Lunch, Dinner];

    public static bool IsValid(string? mealType) =>
        mealType is Lunch or Dinner;
}

public sealed class Recipe
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(600)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = "Abendessen";

    [MaxLength(400)]
    public string Tags { get; set; } = string.Empty;

    public int PrepMinutes { get; set; }
    public int CookMinutes { get; set; }
    public int Servings { get; set; } = 2;

    [MaxLength(16)]
    public string AccentColor { get; set; } = "#DCE5D8";

    [MaxLength(16)]
    public string Emoji { get; set; } = "🍽️";

    public byte[]? ImageData { get; set; }

    [MaxLength(80)]
    public string? ImageContentType { get; set; }

    [MaxLength(600)]
    public string? SourceUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastCookedAtUtc { get; set; }
    public bool IsFavorite { get; set; }

    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
    public ICollection<RecipeStep> Steps { get; set; } = [];
    public ICollection<MealPlanEntry> MealPlanEntries { get; set; } = [];

    public int TotalMinutes => PrepMinutes + CookMinutes;
}

public sealed class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    [MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Aisle { get; set; } = "Sonstiges";

    public int SortOrder { get; set; }
}

public sealed class RecipeStep
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int StepNumber { get; set; }

    [Required, MaxLength(1_500)]
    public string Instruction { get; set; } = string.Empty;
}

public sealed class MealPlanEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }

    [MaxLength(30)]
    public string MealType { get; set; } = MealTypes.Dinner;

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int Servings { get; set; } = 2;
    public bool IsCooked { get; set; }
}

public sealed class ShoppingItemState
{
    public int Id { get; set; }
    public DateOnly WeekStart { get; set; }

    [Required, MaxLength(240)]
    public string ItemKey { get; set; } = string.Empty;

    public bool IsChecked { get; set; }
}

public sealed class HouseholdSettings
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string HouseholdName { get; set; } = "Unser Zuhause";

    [Range(1, 12)]
    public int DefaultServings { get; set; } = 2;

    [Range(10, 180)]
    public int WeeknightMaxMinutes { get; set; } = 35;

    [Range(10, 180)]
    public int WeekendMaxMinutes { get; set; } = 50;

    [Range(1, 7)]
    public int PlannedDinnersPerWeek { get; set; } = 5;

    [Range(0, 7)]
    public int PlannedLunchesPerWeek { get; set; } = 5;

    [Range(0, 60)]
    public int AvoidRepeatsWithinDays { get; set; } = 14;

    [MaxLength(40)]
    public string DietPreference { get; set; } = "Alles";

    [MaxLength(600)]
    public string PreferredTags { get; set; } = "schnell, meal prep";

    [MaxLength(800)]
    public string Allergies { get; set; } = string.Empty;

    [MaxLength(800)]
    public string ExcludedIngredients { get; set; } = string.Empty;
}
