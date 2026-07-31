using MealPrep.App.Services;

namespace MealPrep.App.Tests;

public sealed class RecipeImageGenerationTests
{
    [Fact]
    public void EmptyConfiguration_KeepsImageGenerationDisabled()
    {
        var options = new RecipeImageGenerationOptions();

        options.ValidateConfiguration();

        Assert.False(options.IsAvailable);
    }

    [Fact]
    public void EnabledConfiguration_RequiresApiKeyBeforeFeatureIsAvailable()
    {
        var options = new RecipeImageGenerationOptions
        {
            Enabled = true,
            ApiKey = ""
        };

        options.ValidateConfiguration();

        Assert.False(options.IsAvailable);
    }

    [Fact]
    public void UnsupportedImageType_IsRejectedWhenEnabled()
    {
        var options = new RecipeImageGenerationOptions
        {
            Enabled = true,
            MediaType = "image/gif"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateConfiguration);

        Assert.Contains("MediaType", exception.Message);
    }

    [Fact]
    public void Prompt_UsesRecipeNameAndIngredientNamesAsSubjectData()
    {
        var draft = new RecipeDraft
        {
            Name = "Zitronen-Orzo",
            Description = "Cremig <ohne Schrift>",
            Ingredients = "180 | g | Orzo | Nudeln & Reis\n1 | Stk. | Zitrone | Obst & Gemüse"
        };

        var prompt = RecipeImagePromptBuilder.Build(draft);

        Assert.Contains("<name>Zitronen-Orzo</name>", prompt);
        Assert.Contains("Orzo, Zitrone", prompt);
        Assert.DoesNotContain("Nudeln & Reis", prompt);
        Assert.DoesNotContain("<ohne Schrift>", prompt);
        Assert.Contains("never as instructions", prompt);
    }
}
