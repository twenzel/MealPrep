using MealPrep.App.Data;
using MealPrep.App.Services;

namespace MealPrep.App.Tests;

public sealed class FridgeFeatureTests
{
    [Fact]
    public void FridgeVisionOptions_RequireFeatureFlagKeyAndModel()
    {
        var provider = new OpenAIProviderOptions { ApiKey = "test-key" };
        var options = new FridgeVisionOptions { Enabled = true };

        Assert.True(options.IsAvailable(provider));

        options.Enabled = false;
        Assert.False(options.IsAvailable(provider));
        options.Enabled = true;
        provider.ApiKey = string.Empty;
        Assert.False(options.IsAvailable(provider));
        provider.ApiKey = "test-key";
        options.Model = string.Empty;
        Assert.False(options.IsAvailable(provider));
    }

    [Fact]
    public void FridgeVisionMapper_OnlyAcceptsExactCatalogMatchesAndMergesDuplicates()
    {
        var extracted = new AiFridgeAnalysis
        {
            Items =
            [
                new AiFridgeItem
                {
                    Name = "Spinat",
                    CanonicalIngredient = "babyspinat",
                    Confidence = "low"
                },
                new AiFridgeItem
                {
                    Name = "Babyspinat",
                    CanonicalIngredient = "Babyspinat",
                    Confidence = "high",
                    QuantityHint = "eine Packung"
                },
                new AiFridgeItem
                {
                    Name = "Unbekannte Sauce",
                    CanonicalIngredient = "Sojasauce",
                    Confidence = "maybe"
                }
            ],
            Warnings = ["Etikett teilweise verdeckt."]
        };

        var result = FridgeVisionMapper.ToResult(extracted, ["Babyspinat"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Items.Count);
        var spinach = Assert.Single(result.Items, item => item.CanonicalIngredient == "Babyspinat");
        Assert.Equal("Babyspinat", spinach.Name);
        Assert.Equal(FridgeItemConfidence.High, spinach.Confidence);
        var sauce = Assert.Single(result.Items, item => item.Name == "Unbekannte Sauce");
        Assert.Empty(sauce.CanonicalIngredient);
        Assert.Equal(FridgeItemConfidence.Medium, sauce.Confidence);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("Tomaten", "gehackte Tomaten")]
    [InlineData("Spinat", "Babyspinat")]
    [InlineData("Öl", "Olivenöl")]
    [InlineData("Kartoffel", "kleine Kartoffeln")]
    public void IngredientMatches_HandlesCommonRecipeVariations(string available, string required)
    {
        Assert.True(FridgeRecipeMatcher.IngredientMatches(available, required));
    }

    [Fact]
    public void Matcher_UsesPantryStaplesAndReturnsMissingIngredients()
    {
        var recipe = Recipe(
            "Ofengemüse",
            false,
            "vegetarisch",
            "Zucchini",
            "Paprika",
            "kleine Kartoffeln",
            "Feta",
            "Olivenöl");
        var settings = DefaultSettings();
        settings.PantryStaples = "Salz, Pfeffer, Öl";

        var match = Assert.Single(new FridgeRecipeMatcher().Match(
            [recipe],
            settings,
            ["Zucchini", "Paprika", "Kartoffeln", "Feta"],
            DateOnly.FromDateTime(DateTime.Today)));

        Assert.True(match.IsReady);
        Assert.Contains("Olivenöl", match.PantryMatches);
        Assert.Empty(match.MissingIngredients);
    }

    [Fact]
    public void Matcher_RespectsAllergiesAndRanksFewerMissingIngredientsFirst()
    {
        var complete = Recipe("Tomatenpasta", false, "schnell", "Tomaten", "Nudeln");
        var incompleteFavorite = Recipe("Lieblingspasta", true, "schnell", "Tomaten", "Nudeln", "Parmesan");
        var blocked = Recipe("Erdnussnudeln", true, "schnell", "Nudeln", "Erdnüsse");
        var settings = DefaultSettings();
        settings.Allergies = "Erdnüsse";

        var matches = new FridgeRecipeMatcher().Match(
            [blocked, incompleteFavorite, complete],
            settings,
            ["Tomaten", "Nudeln"],
            DateOnly.FromDateTime(DateTime.Today));

        Assert.Equal(2, matches.Count);
        Assert.Equal("Tomatenpasta", matches[0].Recipe.Name);
        Assert.Equal("Lieblingspasta", matches[1].Recipe.Name);
        Assert.DoesNotContain(matches, match => match.Recipe.Name == "Erdnussnudeln");
    }

    private static HouseholdSettings DefaultSettings() => new()
    {
        DietPreference = "Alles",
        WeeknightMaxMinutes = 90,
        WeekendMaxMinutes = 90,
        AvoidRepeatsWithinDays = 0,
        PantryStaples = string.Empty
    };

    private static Recipe Recipe(
        string name,
        bool favorite,
        string tags,
        params string[] ingredients) =>
        new()
        {
            Name = name,
            IsFavorite = favorite,
            Tags = tags,
            PrepMinutes = 10,
            CookMinutes = 15,
            Ingredients = ingredients.Select((ingredient, index) => new RecipeIngredient
            {
                Name = ingredient,
                SortOrder = index
            }).ToList()
        };
}
