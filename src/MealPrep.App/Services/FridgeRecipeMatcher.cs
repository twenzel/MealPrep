using System.Globalization;
using System.Text;
using MealPrep.App.Data;

namespace MealPrep.App.Services;

public sealed class FridgeRecipeMatcher
{
    private static readonly char[] ListSeparators = [',', ';', '\r', '\n'];

    private static readonly HashSet<string> IgnoredWords = new(StringComparer.Ordinal)
    {
        "bio", "frisch", "frische", "frischer", "frisches", "klein", "kleine", "kleiner",
        "kleines", "gehackt", "gehackte", "gehackter", "hell", "helle", "heller", "griechisch",
        "griechische", "griechischer", "reif", "reife", "reifer", "ganz", "ganze", "ganzer"
    };

    private static readonly IReadOnlyDictionary<string, string> IngredientAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["babyspinat"] = "spinat",
            ["cherrytomate"] = "tomate",
            ["cocktailtomate"] = "tomate",
            ["salatgurke"] = "gurke",
            ["knoblauchzehe"] = "knoblauch",
            ["haehnchenbrust"] = "haehnchen",
            ["huehnerbrust"] = "haehnchen",
            ["lachsfilet"] = "lachs",
            ["kochschinken"] = "schinken",
            ["kochsahne"] = "sahne",
            ["jasminreis"] = "reis",
            ["langkornreis"] = "reis",
            ["olivenoel"] = "oel",
            ["olivenol"] = "ol"
        };

    public IReadOnlyList<FridgeRecipeMatch> Match(
        IEnumerable<Recipe> recipes,
        HouseholdSettings settings,
        IEnumerable<string> detectedIngredients,
        DateOnly date)
    {
        var detected = BuildIngredientSet(detectedIngredients);
        var pantry = BuildIngredientSet(SplitList(settings.PantryStaples));
        var matches = new List<FridgeRecipeMatch>();

        foreach (var recipe in recipes.Where(recipe =>
                     MealPlannerService.IsRecipeAllowed(recipe, settings, date)))
        {
            var ingredients = recipe.Ingredients
                .OrderBy(ingredient => ingredient.SortOrder)
                .Select(ingredient => ingredient.Name.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ingredients.Count == 0)
            {
                continue;
            }

            var found = new List<string>();
            var pantryMatches = new List<string>();
            var missing = new List<string>();
            foreach (var ingredient in ingredients)
            {
                if (detected.Any(item => IngredientMatches(item, ingredient)))
                {
                    found.Add(ingredient);
                }
                else if (pantry.Any(item => IngredientMatches(item, ingredient)))
                {
                    pantryMatches.Add(ingredient);
                }
                else
                {
                    missing.Add(ingredient);
                }
            }

            var covered = found.Count + pantryMatches.Count;
            var coverage = covered / (double)ingredients.Count;
            if (covered == 0 || missing.Count > 3 || (missing.Count > 0 && coverage < 0.4))
            {
                continue;
            }

            var score = coverage * 100
                        - missing.Count * 10
                        + (recipe.IsFavorite ? 8 : 0)
                        + MealPlannerService.PreferenceScore(recipe, settings) * 3;
            matches.Add(new FridgeRecipeMatch(
                recipe,
                found,
                pantryMatches,
                missing,
                coverage,
                score));
        }

        return matches
            .OrderBy(match => match.MissingIngredients.Count)
            .ThenByDescending(match => match.Score)
            .ThenBy(match => match.Recipe.TotalMinutes)
            .ThenBy(match => match.Recipe.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static bool IngredientMatches(string available, string required)
    {
        var availableForms = NormalizeIngredient(available);
        var requiredForms = NormalizeIngredient(required);
        if (availableForms.Count == 0 || requiredForms.Count == 0)
        {
            return false;
        }

        return availableForms.Overlaps(requiredForms) ||
               availableForms.Any(left => requiredForms.Any(right =>
                   left.Length >= 4 && right.Length >= 4 &&
                   (left.Contains(right, StringComparison.Ordinal) ||
                    right.Contains(left, StringComparison.Ordinal))));
    }

    private static HashSet<string> BuildIngredientSet(IEnumerable<string> values) =>
        values
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> NormalizeIngredient(string value)
    {
        var normalized = RemoveDiacritics(value)
            .ToLowerInvariant()
            .Replace('ß', 's')
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal);
        var tokens = normalized
            .Split([' ', '-', '_', '/', '(', ')', ',', '.'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()))
            .Where(token => token.Length > 1 && !IgnoredWords.Contains(token))
            .Select(Singularize)
            .ToList();

        var forms = new HashSet<string>(tokens, StringComparer.Ordinal);
        foreach (var token in tokens.ToArray())
        {
            if (IngredientAliases.TryGetValue(token, out var alias))
            {
                forms.Add(alias);
            }
            else
            {
                foreach (var pair in IngredientAliases)
                {
                    if (token.Contains(pair.Key, StringComparison.Ordinal))
                    {
                        forms.Add(pair.Value);
                    }
                }
            }
        }

        if (tokens.Count > 1)
        {
            forms.Add(string.Concat(tokens));
        }

        return forms;
    }

    private static string Singularize(string value)
    {
        if (value.Length > 6 && value.EndsWith("ern", StringComparison.Ordinal))
        {
            return value[..^1];
        }

        if (value.Length > 5 && value.EndsWith("en", StringComparison.Ordinal))
        {
            return value[..^2];
        }

        if (value.Length > 5 && value.EndsWith('n'))
        {
            return value[..^1];
        }

        if (value.Length > 5 && value.EndsWith('e'))
        {
            return value[..^1];
        }

        return value;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IEnumerable<string> SplitList(string? value) =>
        (value ?? string.Empty)
            .Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record FridgeRecipeMatch(
    Recipe Recipe,
    IReadOnlyList<string> MatchedIngredients,
    IReadOnlyList<string> PantryMatches,
    IReadOnlyList<string> MissingIngredients,
    double Coverage,
    double Score)
{
    public bool IsReady => MissingIngredients.Count == 0;

    public string MatchLabel => MissingIngredients.Count switch
    {
        0 => "Direkt möglich",
        1 => "Nur 1 Zutat fehlt",
        _ => $"{MissingIngredients.Count} Zutaten fehlen"
    };
}
