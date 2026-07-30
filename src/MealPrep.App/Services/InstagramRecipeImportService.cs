using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MealPrep.App.Services;

public sealed class InstagramRecipeImportService(
    IHttpClientFactory httpClientFactory,
    ILogger<InstagramRecipeImportService> logger)
{
    private const int MaxHtmlCharacters = 1_000_000;
    private const int MaxRedirects = 2;

    private static readonly Regex MetaTagRegex = new(
        "<meta\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex AttributeRegex = new(
        "(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\\s*=\\s*(?:\"(?<double>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s>]+))",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex InstagramCaptionWrapperRegex = new(
        ":\\s*[\"“](?<caption>.+)[\"”]\\s*$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public async Task<InstagramImportResult> ImportAsync(
        string sourceUrl,
        string? copiedCaption,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeInstagramUrl(sourceUrl, out var normalizedUrl))
        {
            return InstagramImportResult.Failure(
                "Bitte einen gültigen Link zu einem Instagram-Post oder Reel einfügen.");
        }

        var caption = copiedCaption?.Trim();
        var usedCopiedCaption = !string.IsNullOrWhiteSpace(caption);
        if (!usedCopiedCaption)
        {
            try
            {
                caption = await FetchPublicCaptionAsync(normalizedUrl, cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or TaskCanceledException)
            {
                logger.LogInformation(
                    exception,
                    "Instagram caption could not be read for {InstagramUrl}",
                    normalizedUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(caption))
        {
            return InstagramImportResult.Failure(
                "Instagram hat die Beschreibung nicht freigegeben. Kopiere die Bildunterschrift des Posts und füge sie hier ein.");
        }

        var draft = InstagramCaptionParser.CreateDraft(normalizedUrl, caption);
        return new InstagramImportResult(true, draft, null, usedCopiedCaption);
    }

    public static bool TryNormalizeInstagramUrl(string? value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host is not ("instagram.com" or "www.instagram.com" or "m.instagram.com"))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            segments[0].ToLowerInvariant() is not ("p" or "reel" or "tv") ||
            !Regex.IsMatch(segments[1], "^[a-zA-Z0-9_-]+$"))
        {
            return false;
        }

        normalizedUrl = $"https://www.instagram.com/{segments[0].ToLowerInvariant()}/{segments[1]}/";
        return true;
    }

    private async Task<string?> FetchPublicCaptionAsync(
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("instagram-import");
        var currentUrl = sourceUrl;

        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location;
                if (location is null)
                {
                    return null;
                }

                var nextUrl = location.IsAbsoluteUri
                    ? location.ToString()
                    : new Uri(new Uri(currentUrl), location).ToString();
                if (!TryNormalizeInstagramUrl(nextUrl, out currentUrl))
                {
                    return null;
                }

                continue;
            }

            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentType?.MediaType is not "text/html")
            {
                return null;
            }

            var html = await ReadLimitedStringAsync(response.Content, cancellationToken);
            var description = ExtractMetaContent(html, "og:description")
                              ?? ExtractMetaContent(html, "description");
            return CleanInstagramDescription(description);
        }

        return null;
    }

    private static async Task<string> ReadLimitedStringAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxHtmlCharacters)
        {
            throw new IOException("Instagram response is too large.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[8_192];
        var builder = new StringBuilder();
        while (builder.Length < MaxHtmlCharacters)
        {
            var remaining = Math.Min(buffer.Length, MaxHtmlCharacters - builder.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    private static string? ExtractMetaContent(string html, string metaName)
    {
        foreach (Match tagMatch in MetaTagRegex.Matches(html))
        {
            string? name = null;
            string? content = null;
            foreach (Match attributeMatch in AttributeRegex.Matches(tagMatch.Value))
            {
                var attributeName = attributeMatch.Groups["name"].Value;
                var attributeValue = attributeMatch.Groups["double"].Success
                    ? attributeMatch.Groups["double"].Value
                    : attributeMatch.Groups["single"].Success
                        ? attributeMatch.Groups["single"].Value
                        : attributeMatch.Groups["bare"].Value;

                if (attributeName.Equals("property", StringComparison.OrdinalIgnoreCase) ||
                    attributeName.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    name = attributeValue;
                }
                else if (attributeName.Equals("content", StringComparison.OrdinalIgnoreCase))
                {
                    content = WebUtility.HtmlDecode(attributeValue);
                }
            }

            if (name?.Equals(metaName, StringComparison.OrdinalIgnoreCase) == true &&
                !string.IsNullOrWhiteSpace(content))
            {
                return content.Trim();
            }
        }

        return null;
    }

    private static string? CleanInstagramDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var wrapperMatch = InstagramCaptionWrapperRegex.Match(description);
        var caption = wrapperMatch.Success
            ? wrapperMatch.Groups["caption"].Value
            : description;
        caption = WebUtility.HtmlDecode(caption).Trim();

        return caption.Length < 20 ||
               caption.Equals("Instagram", StringComparison.OrdinalIgnoreCase)
            ? null
            : caption;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
}

public static class InstagramCaptionParser
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Regex HashtagRegex = new(
        "#(?<tag>[\\p{L}\\p{N}_-]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex IngredientRegex = new(
        "^(?<quantity>\\d+(?:[.,]\\d+)?|\\d+\\/\\d+|[½¼¾⅓⅔])\\s*(?<unit>kg|g|mg|l|ml|cl|el|tl|esslöffel|teelöffel|stk\\.?|stück|dose|dosen|bund|prise|zehe|zehen|packung|päckchen)?\\s+(?<name>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SectionPrefixRegex = new(
        "^(?<heading>zutaten|ingredients|zubereitung|anleitung|schritte|instructions|method)\\s*[:\\-–]\\s*(?<rest>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex LeadingMarkerRegex = new(
        "^\\s*(?:[-•·–—✓✔]|\\d+[.)])\\s*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static RecipeDraft CreateDraft(string sourceUrl, string caption)
    {
        var lines = ExpandInlineSectionHeadings(caption)
            .Select(line => line.Replace("\u200B", string.Empty).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var ingredientHeading = lines.FindIndex(IsIngredientHeading);
        var stepHeading = lines.FindIndex(IsStepHeading);
        if (stepHeading >= 0 && ingredientHeading >= stepHeading)
        {
            ingredientHeading = -1;
        }

        var titleIndex = lines.FindIndex(line =>
            !IsIngredientHeading(line) &&
            !IsStepHeading(line) &&
            !line.StartsWith('#') &&
            !Uri.IsWellFormedUriString(line, UriKind.Absolute));
        var title = titleIndex >= 0
            ? CleanTitle(lines[titleIndex])
            : "Instagram-Rezept";

        var ingredientLines = ingredientHeading >= 0
            ? lines.Skip(ingredientHeading + 1)
                .Take((stepHeading >= 0 ? stepHeading : lines.Count) - ingredientHeading - 1)
                .Where(IsContentLine)
                .ToList()
            : FindLikelyIngredientLines(lines);

        var stepLines = stepHeading >= 0
            ? lines.Skip(stepHeading + 1)
                .Where(IsContentLine)
                .Select(CleanListLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList()
            : FindLikelyStepLines(lines);

        var descriptionEnd = ingredientHeading >= 0
            ? ingredientHeading
            : stepHeading >= 0
                ? stepHeading
                : Math.Min(lines.Count, titleIndex + 4);
        var description = lines
            .Skip(Math.Max(0, titleIndex + 1))
            .Take(Math.Max(0, descriptionEnd - titleIndex - 1))
            .Where(IsContentLine)
            .Where(line => !IngredientRegex.IsMatch(CleanListLine(line)))
            .Select(CleanListLine);

        var tags = HashtagRegex.Matches(caption)
            .Select(match => match.Groups["tag"].Value.Replace('_', ' '))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        if (tags.Count == 0)
        {
            tags.Add("instagram");
        }

        return new RecipeDraft
        {
            Name = Truncate(title, 160),
            Description = Truncate(string.Join(" ", description), 600),
            Tags = Truncate(string.Join(", ", tags), 400),
            PrepMinutes = 10,
            CookMinutes = 20,
            Servings = 2,
            Ingredients = string.Join(
                Environment.NewLine,
                ingredientLines.Select(FormatIngredient)),
            Steps = string.Join(Environment.NewLine, stepLines),
            SourceUrl = sourceUrl
        };
    }

    private static IEnumerable<string> ExpandInlineSectionHeadings(string caption)
    {
        foreach (var rawLine in caption.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = SectionPrefixRegex.Match(rawLine.Trim());
            if (match.Success)
            {
                yield return match.Groups["heading"].Value;
                yield return match.Groups["rest"].Value;
            }
            else
            {
                yield return rawLine;
            }
        }
    }

    private static List<string> FindLikelyIngredientLines(IEnumerable<string> lines)
    {
        var candidates = lines
            .Select(CleanListLine)
            .Where(line => IngredientRegex.IsMatch(line))
            .ToList();
        return candidates.Count >= 2 ? candidates : [];
    }

    private static List<string> FindLikelyStepLines(IEnumerable<string> lines) =>
        lines
            .Where(line => Regex.IsMatch(line, "^\\s*\\d+[.)]\\s+"))
            .Select(CleanListLine)
            .Where(line => !IngredientRegex.IsMatch(line))
            .ToList();

    private static bool IsIngredientHeading(string line) =>
        NormalizeHeading(line) is "zutaten" or "ingredients";

    private static bool IsStepHeading(string line) =>
        NormalizeHeading(line) is "zubereitung" or "anleitung" or "schritte" or "instructions" or "method";

    private static string NormalizeHeading(string line) =>
        Regex.Replace(line.ToLowerInvariant(), "[^a-zäöüß]", string.Empty);

    private static bool IsContentLine(string line) =>
        !line.StartsWith('#') &&
        !line.Contains("www.instagram.com", StringComparison.OrdinalIgnoreCase) &&
        !line.Contains("link in bio", StringComparison.OrdinalIgnoreCase);

    private static string CleanTitle(string line)
    {
        var title = HashtagRegex.Replace(CleanListLine(line), string.Empty).Trim();
        title = Regex.Replace(
            title,
            "^(rezept|recipe)\\s*[:\\-–]\\s*",
            string.Empty,
            RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(title) ? "Instagram-Rezept" : title;
    }

    private static string CleanListLine(string line) =>
        LeadingMarkerRegex.Replace(line, string.Empty).Trim();

    private static string FormatIngredient(string line)
    {
        var cleaned = CleanListLine(line);
        var match = IngredientRegex.Match(cleaned);
        if (!match.Success)
        {
            return $"1 | | {cleaned} | Sonstiges";
        }

        var quantity = ParseQuantity(match.Groups["quantity"].Value);
        var unit = NormalizeUnit(match.Groups["unit"].Value);
        return $"{quantity} | {unit} | {match.Groups["name"].Value.Trim()} | Sonstiges";
    }

    private static string ParseQuantity(string value)
    {
        var quantity = value switch
        {
            "½" => 0.5m,
            "¼" => 0.25m,
            "¾" => 0.75m,
            "⅓" => 0.33m,
            "⅔" => 0.67m,
            _ when value.Contains('/') => ParseFraction(value),
            _ when decimal.TryParse(
                value.Replace('.', ','),
                NumberStyles.Number,
                GermanCulture,
                out var parsed) => parsed,
            _ => 1m
        };
        return MealPlannerService.FormatQuantity(quantity);
    }

    private static decimal ParseFraction(string value)
    {
        var parts = value.Split('/');
        return parts.Length == 2 &&
               decimal.TryParse(parts[0], out var numerator) &&
               decimal.TryParse(parts[1], out var denominator) &&
               denominator != 0
            ? numerator / denominator
            : 1m;
    }

    private static string NormalizeUnit(string unit) =>
        unit.Trim().ToLowerInvariant() switch
        {
            "" => "Stk.",
            "el" or "esslöffel" => "EL",
            "tl" or "teelöffel" => "TL",
            "stk" or "stk." or "stück" => "Stk.",
            "kg" => "kg",
            "g" => "g",
            "mg" => "mg",
            "l" => "l",
            "ml" => "ml",
            "cl" => "cl",
            "dose" or "dosen" => "Dose",
            "bund" => "Bund",
            "prise" => "Prise",
            "zehe" or "zehen" => "Zehe",
            "packung" or "päckchen" => "Packung",
            _ => unit.Trim()
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
}

public sealed record InstagramImportResult(
    bool IsSuccess,
    RecipeDraft? Draft,
    string? Error,
    bool UsedCopiedCaption)
{
    public static InstagramImportResult Failure(string error) =>
        new(false, null, error, false);
}
