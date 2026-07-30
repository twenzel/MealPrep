using MealPrep.App.Services;
using MealPrep.App.Data;

namespace MealPrep.App.Tests;

public sealed class MealPlannerServiceTests
{
    [Fact]
    public void MealTypes_ContainsLunchAndDinnerInDisplayOrder()
    {
        Assert.Equal([MealTypes.Lunch, MealTypes.Dinner], MealTypes.All);
        Assert.True(MealTypes.IsValid(MealTypes.Lunch));
        Assert.True(MealTypes.IsValid(MealTypes.Dinner));
        Assert.False(MealTypes.IsValid("Frühstück"));
    }

    [Fact]
    public void CreateRecipeDraft_FormatsIngredientsAndStepsForEditing()
    {
        var recipe = new Recipe
        {
            Name = "Bowl",
            Description = "Frisch",
            Tags = "schnell, vegan",
            PrepMinutes = 12,
            CookMinutes = 8,
            Servings = 3,
            Ingredients =
            [
                new RecipeIngredient
                {
                    Quantity = 1.5m,
                    Unit = "EL",
                    Name = "Tahini",
                    Aisle = "Vorrat",
                    SortOrder = 1
                },
                new RecipeIngredient
                {
                    Quantity = 200m,
                    Unit = "g",
                    Name = "Kichererbsen",
                    Aisle = "Konserven",
                    SortOrder = 0
                }
            ],
            Steps =
            [
                new RecipeStep { StepNumber = 2, Instruction = "Servieren." },
                new RecipeStep { StepNumber = 1, Instruction = "Alles mischen." }
            ]
        };

        var draft = MealPlannerService.CreateRecipeDraft(recipe);

        Assert.Equal("Bowl", draft.Name);
        Assert.Equal(
            $"200 | g | Kichererbsen | Konserven{Environment.NewLine}1,5 | EL | Tahini | Vorrat",
            draft.Ingredients);
        Assert.Equal($"Alles mischen.{Environment.NewLine}Servieren.", draft.Steps);
    }

    [Theory]
    [InlineData(
        "https://www.instagram.com/reel/ABC_123/?utm_source=share",
        "https://www.instagram.com/reel/ABC_123/")]
    [InlineData(
        "https://instagram.com/p/xyz-789/",
        "https://www.instagram.com/p/xyz-789/")]
    public void TryNormalizeInstagramUrl_AcceptsPostAndReelLinks(
        string value,
        string expected)
    {
        var result = InstagramRecipeImportService.TryNormalizeInstagramUrl(
            value,
            out var normalized);

        Assert.True(result);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("http://www.instagram.com/reel/ABC/")]
    [InlineData("https://example.com/reel/ABC/")]
    [InlineData("https://www.instagram.com/accounts/login/")]
    [InlineData("https://www.instagram.com/reel/../../admin")]
    public void TryNormalizeInstagramUrl_RejectsUnsupportedLinks(string value)
    {
        Assert.False(InstagramRecipeImportService.TryNormalizeInstagramUrl(value, out _));
    }

    [Fact]
    public void InstagramCaptionParser_CreatesEditableRecipeDraft()
    {
        const string caption = """
            Zitronen-Pasta
            Schnell und cremig für den Feierabend.

            Zutaten:
            200 g Spaghetti
            1 Zitrone
            2 EL Parmesan

            Zubereitung:
            1. Pasta kochen.
            2. Mit Zitrone und Parmesan vermengen.

            #schnell #vegetarisch
            """;

        var draft = InstagramCaptionParser.CreateDraft(
            "https://www.instagram.com/reel/ABC/",
            caption);

        Assert.Equal("Zitronen-Pasta", draft.Name);
        Assert.Equal("Schnell und cremig für den Feierabend.", draft.Description);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "200 | g | Spaghetti | Sonstiges",
                "1 | Stk. | Zitrone | Sonstiges",
                "2 | EL | Parmesan | Sonstiges"),
            draft.Ingredients);
        Assert.Equal(
            $"Pasta kochen.{Environment.NewLine}Mit Zitrone und Parmesan vermengen.",
            draft.Steps);
        Assert.Equal("schnell, vegetarisch", draft.Tags);
        Assert.Equal("https://www.instagram.com/reel/ABC/", draft.SourceUrl);
    }

    [Theory]
    [InlineData(2026, 7, 27, 2026, 7, 27)]
    [InlineData(2026, 7, 30, 2026, 7, 27)]
    [InlineData(2026, 8, 2, 2026, 7, 27)]
    public void StartOfWeek_ReturnsMonday(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var result = MealPlannerService.StartOfWeek(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1,5", 1.5)]
    [InlineData("0,25", 0.25)]
    public void FormatQuantity_UsesGermanFormatting(string expected, double quantity)
    {
        var result = MealPlannerService.FormatQuantity((decimal)quantity);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsRecipeAllowed_AppliesDietAndAllergyRules()
    {
        var recipe = new Recipe
        {
            Name = "Miso-Lachs",
            Tags = "proteinreich, asiatisch",
            PrepMinutes = 10,
            CookMinutes = 20,
            Ingredients = [new RecipeIngredient { Name = "Lachsfilet" }]
        };

        var vegetarian = new HouseholdSettings
        {
            DietPreference = "Vegetarisch",
            WeeknightMaxMinutes = 45,
            WeekendMaxMinutes = 60
        };
        var allergy = new HouseholdSettings
        {
            DietPreference = "Alles",
            Allergies = "Lachs",
            WeeknightMaxMinutes = 45,
            WeekendMaxMinutes = 60
        };

        Assert.False(MealPlannerService.IsRecipeAllowed(recipe, vegetarian, new DateOnly(2026, 7, 30)));
        Assert.False(MealPlannerService.IsRecipeAllowed(recipe, allergy, new DateOnly(2026, 7, 30)));
    }

    [Fact]
    public void IsRecipeAllowed_UsesWeekendTimeLimit()
    {
        var recipe = new Recipe
        {
            Name = "Sonntagsessen",
            Tags = "vegetarisch",
            PrepMinutes = 20,
            CookMinutes = 40
        };
        var settings = new HouseholdSettings
        {
            WeeknightMaxMinutes = 35,
            WeekendMaxMinutes = 75
        };

        Assert.False(MealPlannerService.IsRecipeAllowed(recipe, settings, new DateOnly(2026, 7, 30)));
        Assert.True(MealPlannerService.IsRecipeAllowed(recipe, settings, new DateOnly(2026, 8, 2)));
    }

    [Fact]
    public void PreferenceScore_CountsMatchingPreferredTags()
    {
        var recipe = new Recipe { Tags = "schnell, one pot, vegetarisch" };
        var settings = new HouseholdSettings { PreferredTags = "Schnell, One Pot, Meal Prep" };

        Assert.Equal(2, MealPlannerService.PreferenceScore(recipe, settings));
    }

    [Fact]
    public void PickSurpriseRecipe_UsesAnUnusedAllowedRecipe()
    {
        var usedRecipe = new Recipe
        {
            Id = 1,
            Name = "Schon geplant",
            Tags = "schnell",
            PrepMinutes = 10,
            CookMinutes = 10
        };
        var unusedRecipe = new Recipe
        {
            Id = 2,
            Name = "Neue Überraschung",
            Tags = "schnell",
            PrepMinutes = 10,
            CookMinutes = 10
        };
        var tooSlowRecipe = new Recipe
        {
            Id = 3,
            Name = "Zu aufwendig",
            Tags = "schnell",
            PrepMinutes = 60,
            CookMinutes = 60
        };
        var settings = new HouseholdSettings
        {
            WeeknightMaxMinutes = 30,
            WeekendMaxMinutes = 30
        };

        var selected = MealPlannerService.PickSurpriseRecipe(
            [usedRecipe, unusedRecipe, tooSlowRecipe],
            settings,
            new DateOnly(2026, 7, 30),
            new HashSet<int> { usedRecipe.Id },
            new Random(42));

        Assert.Same(unusedRecipe, selected);
    }

    [Fact]
    public void PickSurpriseRecipe_ReturnsNullWhenNothingMatches()
    {
        var recipe = new Recipe
        {
            Id = 1,
            Name = "Fischgericht",
            Tags = "proteinreich",
            PrepMinutes = 10,
            CookMinutes = 10,
            Ingredients = [new RecipeIngredient { Name = "Lachs" }]
        };
        var settings = new HouseholdSettings
        {
            Allergies = "Lachs",
            WeeknightMaxMinutes = 30,
            WeekendMaxMinutes = 30
        };

        var selected = MealPlannerService.PickSurpriseRecipe(
            [recipe],
            settings,
            new DateOnly(2026, 7, 30),
            new HashSet<int>(),
            new Random(42));

        Assert.Null(selected);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.4+Branch.main.Sha.abc123", "1.2.3-beta.4")]
    [InlineData(null, "9.8.7")]
    public void ApplicationVersion_UsesInformationalVersionWithoutBuildMetadata(
        string? informationalVersion,
        string expected)
    {
        var result = ApplicationVersion.ToDisplayVersion(
            informationalVersion,
            new Version(9, 8, 7, 6));

        Assert.Equal(expected, result);
    }
}
