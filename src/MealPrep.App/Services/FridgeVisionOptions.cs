namespace MealPrep.App.Services;

public sealed class FridgeVisionOptions
{
    public const string SectionName = "AI:FridgeVision";

    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-5.6-terra";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaximumImages { get; set; } = 3;
    public int MaximumSourceImageBytes { get; set; } = 20 * 1024 * 1024;
    public int MaximumImageBytes { get; set; } = 5 * 1024 * 1024;
    public int ResizeMaxDimension { get; set; } = 2048;

    public bool IsAvailable(OpenAIProviderOptions provider) =>
        Enabled && provider.IsConfigured && !string.IsNullOrWhiteSpace(Model);

    public void ValidateConfiguration()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException($"{SectionName}:Model darf nicht leer sein.");
        }

        if (TimeoutSeconds is < 10 or > 300)
        {
            throw new InvalidOperationException(
                $"{SectionName}:TimeoutSeconds muss zwischen 10 und 300 liegen.");
        }

        if (MaximumImages is < 1 or > 5)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumImages muss zwischen 1 und 5 liegen.");
        }

        if (MaximumSourceImageBytes is < 1024 * 1024 or > 50 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumSourceImageBytes muss zwischen 1 MB und 50 MB liegen.");
        }

        if (MaximumImageBytes is < 256 * 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumImageBytes muss zwischen 256 KB und 20 MB liegen.");
        }

        if (ResizeMaxDimension is < 512 or > 4096)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ResizeMaxDimension muss zwischen 512 und 4096 liegen.");
        }
    }
}

public sealed record FridgePhoto(byte[] Data, string MediaType)
{
    public string DataUrl => $"data:{MediaType};base64,{Convert.ToBase64String(Data)}";
}

public static class FridgeItemConfidence
{
    public const string High = "Hoch";
    public const string Medium = "Mittel";
    public const string Low = "Niedrig";
}

public sealed class DetectedFridgeItem
{
    public string Name { get; set; } = string.Empty;
    public string CanonicalIngredient { get; set; } = string.Empty;
    public string Confidence { get; set; } = FridgeItemConfidence.Medium;
    public string QuantityHint { get; set; } = string.Empty;
}

public sealed record FridgeVisionResult(
    bool IsSuccess,
    IReadOnlyList<DetectedFridgeItem> Items,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public static FridgeVisionResult Success(
        IReadOnlyList<DetectedFridgeItem> items,
        IReadOnlyList<string> warnings) =>
        new(true, items, warnings, null);

    public static FridgeVisionResult Failure(string error) =>
        new(false, [], [], error);
}
