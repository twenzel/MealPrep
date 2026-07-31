namespace MealPrep.App.Services;

public sealed class RecipeImageGenerationOptions
{
    public const string SectionName = "AI:RecipeImages";
    public const int DefaultMaximumImageBytes = 4 * 1024 * 1024;

    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-image-1";
    public string? ApiKey { get; set; }
    public string MediaType { get; set; } = "image/png";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaximumImageBytes { get; set; } = DefaultMaximumImageBytes;

    public bool IsAvailable =>
        Enabled &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);

    public void ValidateConfiguration()
    {
        if (!Enabled)
        {
            return;
        }

        if (TimeoutSeconds is < 10 or > 300)
        {
            throw new InvalidOperationException(
                $"{SectionName}:TimeoutSeconds muss zwischen 10 und 300 liegen.");
        }

        if (MaximumImageBytes is < 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumImageBytes muss zwischen 1 KB und 20 MB liegen.");
        }

        if (!RecipeImageGenerationResult.AllowedMediaTypes.Contains(MediaType))
        {
            throw new InvalidOperationException(
                $"{SectionName}:MediaType muss image/jpeg, image/png oder image/webp sein.");
        }
    }
}

public sealed record RecipeImageGenerationResult(
    bool IsSuccess,
    byte[]? ImageData,
    string? MediaType,
    string? Error)
{
    public static readonly IReadOnlySet<string> AllowedMediaTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    public static RecipeImageGenerationResult Success(byte[] imageData, string mediaType) =>
        new(true, imageData, mediaType, null);

    public static RecipeImageGenerationResult Failure(string error) =>
        new(false, null, null, error);
}
