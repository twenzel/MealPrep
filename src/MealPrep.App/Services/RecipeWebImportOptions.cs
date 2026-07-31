namespace MealPrep.App.Services;

public sealed class OpenAIProviderOptions
{
    public const string SectionName = "AI:OpenAI";

    public string? ApiKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class RecipeWebImportOptions
{
    public const string SectionName = "AI:RecipeImport";

    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-5.6-terra";
    public int HttpTimeoutSeconds { get; set; } = 20;
    public int AiTimeoutSeconds { get; set; } = 90;
    public int MaximumHtmlBytes { get; set; } = 2 * 1024 * 1024;
    public int MaximumTextCharacters { get; set; } = 30_000;
    public int MaximumImageBytes { get; set; } = 4 * 1024 * 1024;
    public int MaximumRedirects { get; set; } = 3;

    public bool IsAvailable(OpenAIProviderOptions provider) =>
        Enabled && provider.IsConfigured && !string.IsNullOrWhiteSpace(Model);

    public void ValidateConfiguration()
    {
        if (!Enabled)
        {
            return;
        }

        if (HttpTimeoutSeconds is < 5 or > 60)
        {
            throw new InvalidOperationException(
                $"{SectionName}:HttpTimeoutSeconds muss zwischen 5 und 60 liegen.");
        }

        if (AiTimeoutSeconds is < 10 or > 300)
        {
            throw new InvalidOperationException(
                $"{SectionName}:AiTimeoutSeconds muss zwischen 10 und 300 liegen.");
        }

        if (MaximumHtmlBytes is < 64 * 1024 or > 5 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumHtmlBytes muss zwischen 64 KB und 5 MB liegen.");
        }

        if (MaximumTextCharacters is < 2_000 or > 100_000)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumTextCharacters muss zwischen 2.000 und 100.000 liegen.");
        }

        if (MaximumImageBytes is < 64 * 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumImageBytes muss zwischen 64 KB und 20 MB liegen.");
        }

        if (MaximumRedirects is < 0 or > 5)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumRedirects muss zwischen 0 und 5 liegen.");
        }
    }
}

public sealed record ImportedRecipeImage(byte[] Data, string MediaType, string SourceUrl)
{
    public string DataUrl => $"data:{MediaType};base64,{Convert.ToBase64String(Data)}";
}

public sealed record RecipeWebImportResult(
    bool IsSuccess,
    RecipeDraft? Draft,
    ImportedRecipeImage? SuggestedImage,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public static RecipeWebImportResult Success(
        RecipeDraft draft,
        ImportedRecipeImage? suggestedImage,
        IReadOnlyList<string> warnings) =>
        new(true, draft, suggestedImage, warnings, null);

    public static RecipeWebImportResult Failure(string error) =>
        new(false, null, null, [], error);
}

public sealed class WebRecipeImportException(string message) : Exception(message);
