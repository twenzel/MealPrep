using MealPrep.App.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MealPrep.App.Services;

public sealed class MealPlannerService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    public async Task<List<Recipe>> GetRecipesAsync(string? search = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Recipes
            .Include(recipe => recipe.Ingredients.OrderBy(ingredient => ingredient.SortOrder))
            .Include(recipe => recipe.Steps.OrderBy(step => step.StepNumber))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(recipe =>
                recipe.Name.ToLower().Contains(term) ||
                recipe.Tags.ToLower().Contains(term) ||
                recipe.Description.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(recipe => recipe.IsFavorite)
            .ThenBy(recipe => recipe.Name)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(recipe => recipe.Ingredients.OrderBy(ingredient => ingredient.SortOrder))
            .Include(recipe => recipe.Steps.OrderBy(step => step.StepNumber))
            .AsNoTracking()
            .SingleOrDefaultAsync(recipe => recipe.Id == id);
    }

    public async Task<bool?> ToggleFavoriteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var recipe = await db.Recipes.SingleOrDefaultAsync(item => item.Id == id);
        if (recipe is null)
        {
            return null;
        }

        recipe.IsFavorite = !recipe.IsFavorite;
        await db.SaveChangesAsync();
        return recipe.IsFavorite;
    }

    public async Task<List<MealPlanEntry>> GetWeekPlanAsync(DateOnly weekStart)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var weekEnd = weekStart.AddDays(7);
        return await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .Where(entry => entry.Date >= weekStart && entry.Date < weekEnd)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.MealType)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<MealPlanEntry?> GetMealAsync(DateOnly date, string mealType = MealTypes.Dinner)
    {
        EnsureValidMealType(mealType);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Date == date && entry.MealType == mealType);
    }

    public async Task AssignRecipeAsync(
        DateOnly date,
        string mealType,
        int recipeId,
        int? servings = null)
    {
        EnsureValidMealType(mealType);
        await using var db = await dbFactory.CreateDbContextAsync();
        var plannedServings = servings ?? await db.HouseholdSettings
            .Select(settings => settings.DefaultServings)
            .FirstOrDefaultAsync();
        plannedServings = Math.Clamp(plannedServings == 0 ? 2 : plannedServings, 1, 12);

        var entry = await db.MealPlanEntries
            .SingleOrDefaultAsync(item => item.Date == date && item.MealType == mealType);

        if (entry is null)
        {
            db.MealPlanEntries.Add(new MealPlanEntry
            {
                Date = date,
                MealType = mealType,
                RecipeId = recipeId,
                Servings = plannedServings
            });
        }
        else
        {
            entry.RecipeId = recipeId;
            entry.Servings = plannedServings;
            entry.IsCooked = false;
        }

        await db.SaveChangesAsync();
    }

    public async Task<HouseholdSettings> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.HouseholdSettings.AsNoTracking().FirstOrDefaultAsync()
               ?? new HouseholdSettings();
    }

    public async Task<HouseholdSettings> SaveSettingsAsync(HouseholdSettings input)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.HouseholdSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new HouseholdSettings();
            db.HouseholdSettings.Add(settings);
        }

        settings.HouseholdName = string.IsNullOrWhiteSpace(input.HouseholdName)
            ? "Unser Zuhause"
            : input.HouseholdName.Trim();
        settings.DefaultServings = Math.Clamp(input.DefaultServings, 1, 12);
        settings.WeeknightMaxMinutes = Math.Clamp(input.WeeknightMaxMinutes, 10, 180);
        settings.WeekendMaxMinutes = Math.Clamp(input.WeekendMaxMinutes, 10, 180);
        settings.PlannedDinnersPerWeek = Math.Clamp(input.PlannedDinnersPerWeek, 1, 7);
        settings.PlannedLunchesPerWeek = Math.Clamp(input.PlannedLunchesPerWeek, 0, 7);
        settings.AvoidRepeatsWithinDays = Math.Clamp(input.AvoidRepeatsWithinDays, 0, 60);
        settings.DietPreference = NormalizeDiet(input.DietPreference);
        settings.PreferredTags = NormalizeList(input.PreferredTags);
        settings.Allergies = NormalizeList(input.Allergies);
        settings.ExcludedIngredients = NormalizeList(input.ExcludedIngredients);

        await db.SaveChangesAsync();
        return await db.HouseholdSettings.AsNoTracking().SingleAsync();
    }

    public async Task RemoveMealAsync(DateOnly date, string mealType)
    {
        EnsureValidMealType(mealType);
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.MealPlanEntries
            .SingleOrDefaultAsync(item => item.Date == date && item.MealType == mealType);
        if (entry is null)
        {
            return;
        }

        db.MealPlanEntries.Remove(entry);
        await db.SaveChangesAsync();
    }

    public async Task ToggleCookedAsync(int entryId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.MealPlanEntries.Include(item => item.Recipe)
            .SingleAsync(item => item.Id == entryId);
        entry.IsCooked = !entry.IsCooked;
        entry.Recipe.LastCookedAtUtc = entry.IsCooked ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
    }

    public async Task MarkCookedAsync(
        int recipeId,
        DateOnly date,
        string mealType = MealTypes.Dinner)
    {
        EnsureValidMealType(mealType);
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.MealPlanEntries
            .Include(item => item.Recipe)
            .SingleOrDefaultAsync(item =>
                item.Date == date &&
                item.RecipeId == recipeId &&
                item.MealType == mealType);
        if (entry is null || entry.IsCooked)
        {
            return;
        }

        entry.IsCooked = true;
        entry.Recipe.LastCookedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<int> AddRecipeAsync(RecipeDraft draft)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var recipe = new Recipe
        {
            Category = "Abendessen",
            AccentColor = "#DFE7D5",
            Emoji = "🥣"
        };

        ApplyDraft(recipe, draft);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }

    public async Task<bool> UpdateRecipeAsync(int id, RecipeDraft draft)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var recipe = await db.Recipes
            .Include(item => item.Ingredients)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (recipe is null)
        {
            return false;
        }

        db.RecipeIngredients.RemoveRange(recipe.Ingredients);
        db.RecipeSteps.RemoveRange(recipe.Steps);
        recipe.Ingredients.Clear();
        recipe.Steps.Clear();
        ApplyDraft(recipe, draft);

        await db.SaveChangesAsync();
        return true;
    }

    public static RecipeDraft CreateRecipeDraft(Recipe recipe) =>
        new()
        {
            Name = recipe.Name,
            Description = recipe.Description,
            Tags = recipe.Tags,
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
            Servings = recipe.Servings,
            Ingredients = string.Join(
                Environment.NewLine,
                recipe.Ingredients
                    .OrderBy(ingredient => ingredient.SortOrder)
                    .Select(ingredient =>
                        $"{FormatQuantity(ingredient.Quantity)} | {ingredient.Unit} | {ingredient.Name} | {ingredient.Aisle}")),
            Steps = string.Join(
                Environment.NewLine,
                recipe.Steps
                    .OrderBy(step => step.StepNumber)
                    .Select(step => step.Instruction)),
            ImageData = recipe.ImageData,
            ImageContentType = recipe.ImageContentType,
            SourceUrl = recipe.SourceUrl
        };

    private static void ApplyDraft(Recipe recipe, RecipeDraft draft)
    {
        recipe.Name = draft.Name.Trim();
        recipe.Description = draft.Description.Trim();
        recipe.Tags = draft.Tags.Trim();
        recipe.PrepMinutes = Math.Clamp(draft.PrepMinutes, 0, 720);
        recipe.CookMinutes = Math.Clamp(draft.CookMinutes, 0, 720);
        recipe.Servings = Math.Clamp(draft.Servings, 1, 12);
        recipe.ImageData = draft.ImageData;
        recipe.ImageContentType = draft.ImageData is null ? null : draft.ImageContentType;
        recipe.SourceUrl = string.IsNullOrWhiteSpace(draft.SourceUrl)
            ? null
            : draft.SourceUrl.Trim();

        var ingredientLines = draft.Ingredients
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < ingredientLines.Length; index++)
        {
            recipe.Ingredients.Add(ParseIngredient(ingredientLines[index], index));
        }

        var stepLines = draft.Steps
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < stepLines.Length; index++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = index + 1,
                Instruction = stepLines[index]
            });
        }
    }

    public async Task<List<ShoppingListItem>> GetShoppingListAsync(DateOnly weekStart)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var weekEnd = weekStart.AddDays(7);
        var meals = await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .ThenInclude(recipe => recipe.Ingredients)
            .Where(entry => entry.Date >= weekStart && entry.Date < weekEnd)
            .AsNoTracking()
            .ToListAsync();

        var states = await db.ShoppingItemStates
            .Where(state => state.WeekStart == weekStart)
            .AsNoTracking()
            .ToDictionaryAsync(state => state.ItemKey, state => state.IsChecked);

        return meals
            .SelectMany(entry => entry.Recipe.Ingredients.Select(ingredient => new
            {
                Ingredient = ingredient,
                Factor = entry.Servings / (decimal)Math.Max(1, entry.Recipe.Servings),
                RecipeName = entry.Recipe.Name
            }))
            .GroupBy(item => BuildShoppingKey(item.Ingredient))
            .Select(group =>
            {
                var first = group.First().Ingredient;
                var quantity = group.Sum(item => item.Ingredient.Quantity * item.Factor);
                var key = group.Key;
                return new ShoppingListItem(
                    key,
                    first.Name,
                    quantity,
                    first.Unit,
                    first.Aisle,
                    states.GetValueOrDefault(key),
                    string.Join(", ", group.Select(item => item.RecipeName).Distinct()));
            })
            .OrderBy(item => item.IsChecked)
            .ThenBy(item => item.Aisle)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public async Task ToggleShoppingItemAsync(DateOnly weekStart, string itemKey)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var state = await db.ShoppingItemStates
            .SingleOrDefaultAsync(item => item.WeekStart == weekStart && item.ItemKey == itemKey);
        if (state is null)
        {
            db.ShoppingItemStates.Add(new ShoppingItemState
            {
                WeekStart = weekStart,
                ItemKey = itemKey,
                IsChecked = true
            });
        }
        else
        {
            state.IsChecked = !state.IsChecked;
        }

        await db.SaveChangesAsync();
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    public static string FormatQuantity(decimal quantity)
    {
        return quantity == decimal.Truncate(quantity)
            ? decimal.Truncate(quantity).ToString("0", GermanCulture)
            : quantity.ToString("0.##", GermanCulture);
    }

    public static bool IsRecipeAllowed(Recipe recipe, HouseholdSettings settings, DateOnly date)
    {
        var maxMinutes = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? settings.WeekendMaxMinutes
            : settings.WeeknightMaxMinutes;
        if (recipe.TotalMinutes > maxMinutes)
        {
            return false;
        }

        if (settings.AvoidRepeatsWithinDays > 0 &&
            recipe.LastCookedAtUtc is not null &&
            recipe.LastCookedAtUtc >= DateTime.UtcNow.AddDays(-settings.AvoidRepeatsWithinDays))
        {
            return false;
        }

        var tags = recipe.Tags.ToLowerInvariant();
        if (settings.DietPreference.Equals("Vegan", StringComparison.OrdinalIgnoreCase) &&
            !tags.Contains("vegan"))
        {
            return false;
        }

        if (settings.DietPreference.Equals("Vegetarisch", StringComparison.OrdinalIgnoreCase) &&
            !tags.Contains("vegetarisch") &&
            !tags.Contains("vegan"))
        {
            return false;
        }

        var blockedIngredients = SplitList(settings.Allergies)
            .Concat(SplitList(settings.ExcludedIngredients))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return blockedIngredients.Count == 0 ||
               recipe.Ingredients.All(ingredient =>
                   blockedIngredients.All(blocked =>
                       !ingredient.Name.Contains(blocked, StringComparison.OrdinalIgnoreCase)));
    }

    public static int PreferenceScore(Recipe recipe, HouseholdSettings settings)
    {
        return SplitList(settings.PreferredTags)
            .Count(tag => recipe.Tags.Contains(tag, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildShoppingKey(RecipeIngredient ingredient) =>
        $"{ingredient.Name.Trim().ToLowerInvariant()}|{ingredient.Unit.Trim().ToLowerInvariant()}";

    private static void EnsureValidMealType(string mealType)
    {
        if (!MealTypes.IsValid(mealType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mealType),
                mealType,
                "Die Mahlzeit muss Mittagessen oder Abendessen sein.");
        }
    }

    private static string NormalizeDiet(string? value) =>
        value?.Trim() switch
        {
            "Vegetarisch" => "Vegetarisch",
            "Vegan" => "Vegan",
            _ => "Alles"
        };

    private static string NormalizeList(string? value) =>
        string.Join(", ", SplitList(value).Distinct(StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitList(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));

    private static RecipeIngredient ParseIngredient(string line, int sortOrder)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        var quantity = parts.Length > 0 &&
                       decimal.TryParse(parts[0], NumberStyles.Number, GermanCulture, out var parsed)
            ? parsed
            : 1;

        return new RecipeIngredient
        {
            Quantity = quantity,
            Unit = parts.Length > 1 ? parts[1] : string.Empty,
            Name = parts.Length > 2 ? parts[2] : line,
            Aisle = parts.Length > 3 ? parts[3] : "Sonstiges",
            SortOrder = sortOrder
        };
    }
}

public sealed class RecipeDraft
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int PrepMinutes { get; set; } = 10;
    public int CookMinutes { get; set; } = 20;
    public int Servings { get; set; } = 2;
    public string Ingredients { get; set; } = string.Empty;
    public string Steps { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public string? SourceUrl { get; set; }
}

public sealed record ShoppingListItem(
    string Key,
    string Name,
    decimal Quantity,
    string Unit,
    string Aisle,
    bool IsChecked,
    string UsedBy);
