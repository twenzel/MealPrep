using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace MealPrep.App.Services;

public sealed class RecipePageExtractor(RecipeWebImportOptions options)
{
    private static readonly Regex WhitespaceRegex = new(
        "\\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex NumberRegex = new(
        "\\d+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public RecipePageContent Extract(FetchedWebPage page)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(page.Html);
        var structured = ExtractStructuredRecipe(document);
        var title = FirstNonEmpty(
            structured?.Name,
            ReadMeta(document, "property", "og:title"),
            document.Title);
        var description = FirstNonEmpty(
            structured?.Description,
            ReadMeta(document, "property", "og:description"),
            ReadMeta(document, "name", "description"));
        var visibleText = BuildVisibleText(document);

        var imageCandidates = new List<string>();
        if (structured is not null)
        {
            foreach (var imageUrl in structured.ImageUrls)
            {
                AddImageCandidate(imageCandidates, page.FinalUri, imageUrl);
            }
        }

        AddImageCandidate(imageCandidates, page.FinalUri, ReadMeta(document, "property", "og:image"));
        AddImageCandidate(imageCandidates, page.FinalUri, ReadMeta(document, "name", "twitter:image"));

        var contentRoot = FindContentRoot(document);
        foreach (var image in contentRoot.QuerySelectorAll("img").Take(12))
        {
            var source = image.GetAttribute("data-src") ?? image.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(source))
            {
                source = GetLastSrcSetCandidate(image.GetAttribute("srcset"));
            }

            AddImageCandidate(imageCandidates, page.FinalUri, source);
        }

        return new RecipePageContent(
            page.FinalUri,
            Truncate(NormalizeText(title), 300),
            Truncate(NormalizeText(description), 1_000),
            Truncate(visibleText, options.MaximumTextCharacters),
            structured,
            imageCandidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray());
    }

    private static StructuredRecipeData? ExtractStructuredRecipe(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            var json = script.TextContent.Trim();
            if (json.StartsWith("<!--", StringComparison.Ordinal))
            {
                json = json[4..];
            }

            if (json.EndsWith("-->", StringComparison.Ordinal))
            {
                json = json[..^3];
            }

            try
            {
                using var parsed = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                        MaxDepth = 64
                    });
                foreach (var candidate in EnumerateObjects(parsed.RootElement))
                {
                    if (!HasRecipeType(candidate))
                    {
                        continue;
                    }

                    var recipe = ParseRecipe(candidate);
                    if (!string.IsNullOrWhiteSpace(recipe.Name) || recipe.Ingredients.Count > 0)
                    {
                        return recipe;
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON-LD is ignored; visible page text remains available.
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateObjects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in EnumerateObjects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool HasRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => type.GetString()?.Equals(
                "Recipe",
                StringComparison.OrdinalIgnoreCase) == true,
            JsonValueKind.Array => type.EnumerateArray().Any(value =>
                value.ValueKind == JsonValueKind.String &&
                value.GetString()?.Equals("Recipe", StringComparison.OrdinalIgnoreCase) == true),
            _ => false
        };
    }

    private static StructuredRecipeData ParseRecipe(JsonElement recipe)
    {
        var ingredients = new List<string>();
        if (recipe.TryGetProperty("recipeIngredient", out var ingredientValue))
        {
            ReadTextValues(ingredientValue, ingredients);
        }

        var steps = new List<string>();
        if (recipe.TryGetProperty("recipeInstructions", out var instructionValue))
        {
            ReadInstructionValues(instructionValue, steps);
        }

        var tags = new List<string>();
        if (recipe.TryGetProperty("keywords", out var keywords))
        {
            ReadTags(keywords, tags);
        }

        if (recipe.TryGetProperty("recipeCategory", out var category))
        {
            ReadTags(category, tags);
        }

        var images = new List<string>();
        if (recipe.TryGetProperty("image", out var image))
        {
            ReadImageUrls(image, images);
        }

        return new StructuredRecipeData
        {
            Name = ReadTextProperty(recipe, "name"),
            Description = ReadTextProperty(recipe, "description"),
            PrepMinutes = ReadDurationMinutes(recipe, "prepTime"),
            CookMinutes = ReadDurationMinutes(recipe, "cookTime"),
            Servings = ReadServings(recipe),
            Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(),
            Ingredients = ingredients.Where(value => !string.IsNullOrWhiteSpace(value)).Take(100).ToArray(),
            Steps = steps.Where(value => !string.IsNullOrWhiteSpace(value)).Take(80).ToArray(),
            ImageUrls = images.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray()
        };
    }

    private static void ReadTextValues(JsonElement value, List<string> destination)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                destination.Add(NormalizeText(value.GetString()));
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ReadTextValues(item, destination);
                }

                break;
            case JsonValueKind.Object:
                if (value.TryGetProperty("itemListElement", out var elements))
                {
                    ReadTextValues(elements, destination);
                    break;
                }

                var name = ReadTextProperty(value, "name");
                var amount = ReadTextProperty(value, "value");
                var unit = FirstNonEmpty(
                    ReadTextProperty(value, "unitText"),
                    ReadTextProperty(value, "unitCode"));
                var combined = NormalizeText(string.Join(
                    " ",
                    new[] { amount, unit, name }.Where(item => !string.IsNullOrWhiteSpace(item))));
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    destination.Add(combined);
                }

                break;
        }
    }

    private static void ReadInstructionValues(JsonElement value, List<string> destination)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var line in (value.GetString() ?? string.Empty).Split(
                             ['\r', '\n'],
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    destination.Add(NormalizeText(line));
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ReadInstructionValues(item, destination);
                }

                break;
            case JsonValueKind.Object:
                if (value.TryGetProperty("itemListElement", out var elements))
                {
                    ReadInstructionValues(elements, destination);
                }
                else
                {
                    var text = FirstNonEmpty(
                        ReadTextProperty(value, "text"),
                        ReadTextProperty(value, "name"));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        destination.Add(text);
                    }
                }

                break;
        }
    }

    private static void ReadTags(JsonElement value, List<string> destination)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ReadTags(item, destination);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return;
        }

        destination.AddRange((value.GetString() ?? string.Empty).Split(
            [',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static void ReadImageUrls(JsonElement value, List<string> destination)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                destination.Add(value.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ReadImageUrls(item, destination);
                }

                break;
            case JsonValueKind.Object:
                var url = FirstNonEmpty(
                    ReadTextProperty(value, "url"),
                    ReadTextProperty(value, "contentUrl"));
                if (!string.IsNullOrWhiteSpace(url))
                {
                    destination.Add(url);
                }

                break;
        }
    }

    private static int? ReadDurationMinutes(JsonElement recipe, string propertyName)
    {
        var duration = ReadTextProperty(recipe, propertyName);
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        try
        {
            return Math.Max(0, (int)Math.Round(XmlConvert.ToTimeSpan(duration).TotalMinutes));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static int? ReadServings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var yield))
        {
            return null;
        }

        var value = yield.ValueKind == JsonValueKind.Array
            ? yield.EnumerateArray().FirstOrDefault().ToString()
            : yield.ToString();
        var match = NumberRegex.Match(value);
        return match.Success && int.TryParse(match.Value, out var servings)
            ? servings
            : null;
    }

    private static string ReadTextProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => NormalizeText(PlainText(value.GetString())),
            JsonValueKind.Number => value.ToString(),
            _ => string.Empty
        };
    }

    private static string BuildVisibleText(IDocument document)
    {
        foreach (var node in document.QuerySelectorAll(
                     "script,style,noscript,svg,nav,footer,aside,form,dialog,[aria-hidden='true']").ToArray())
        {
            node.Remove();
        }

        var root = FindContentRoot(document);
        var lines = root.QuerySelectorAll("h1,h2,h3,h4,p,li")
            .Select(element => NormalizeText(element.TextContent))
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var text = lines.Length >= 4
            ? string.Join(Environment.NewLine, lines)
            : NormalizeText(root.TextContent);
        return text;
    }

    private static IElement FindContentRoot(IDocument document)
    {
        var candidates = document.QuerySelectorAll(
                "[itemtype*='schema.org/Recipe'],main,article,[role='main'],.recipe,#recipe")
            .Where(element => element.TextContent.Length > 100)
            .OrderByDescending(element => element.TextContent.Length)
            .ToArray();
        return candidates.FirstOrDefault() ?? document.Body ?? document.DocumentElement!;
    }

    private static string ReadMeta(IDocument document, string attribute, string value) =>
        document.QuerySelector($"meta[{attribute}='{value}']")?.GetAttribute("content") ?? string.Empty;

    private static void AddImageCandidate(List<string> destination, Uri baseUri, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(baseUri, value.Trim(), out var candidate) ||
            !WebImportUrl.TryNormalizeHttps(candidate.ToString(), out var normalized))
        {
            return;
        }

        destination.Add(normalized.ToString());
    }

    private static string GetLastSrcSetCandidate(string? srcSet)
    {
        var candidate = srcSet?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return candidate?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
               ?? string.Empty;
    }

    private static string PlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('<'))
        {
            return value ?? string.Empty;
        }

        return new HtmlParser().ParseDocument(value).Body?.TextContent ?? value;
    }

    private static string NormalizeText(string? value) =>
        WhitespaceRegex.Replace(value ?? string.Empty, " ").Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
        ?? string.Empty;

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength].TrimEnd();
}

public sealed record RecipePageContent(
    Uri SourceUri,
    string Title,
    string Description,
    string VisibleText,
    StructuredRecipeData? StructuredRecipe,
    IReadOnlyList<string> ImageCandidates);

public sealed class StructuredRecipeData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public int? Servings { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
    public IReadOnlyList<string> Ingredients { get; set; } = [];
    public IReadOnlyList<string> Steps { get; set; } = [];
    public IReadOnlyList<string> ImageUrls { get; set; } = [];
}
