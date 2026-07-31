using MealPrep.App.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPrep.App.Services;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Recipes.AnyAsync())
        {
            return;
        }

        var recipes = new[]
        {
            Recipe(
                "Zitronen-Orzo mit Spinat",
                "Cremig, frisch und in einem Topf fertig – genau richtig für einen entspannten Montag.",
                "schnell, vegetarisch, one pot",
                8, 17, "🍋", "#DCE3A7", "orzo.jpg",
                [
                    (180m, "g", "Orzo", "Nudeln & Reis"),
                    (120m, "g", "Babyspinat", "Obst & Gemüse"),
                    (1m, "Stk.", "Bio-Zitrone", "Obst & Gemüse"),
                    (40m, "g", "Parmesan", "Kühlregal"),
                    (500m, "ml", "Gemüsebrühe", "Vorrat")
                ],
                [
                    "Orzo mit der Gemüsebrühe in einen weiten Topf geben und bei mittlerer Hitze köcheln lassen.",
                    "Spinat unterheben, bis er zusammenfällt.",
                    "Zitronenabrieb, etwas Saft und Parmesan einrühren. Abschmecken und direkt servieren."
                ]),
            Recipe(
                "Miso-Lachs mit Brokkoli",
                "Glasierter Lachs, knackiger Brokkoli und Reis aus einer Schüssel.",
                "proteinreich, asiatisch, unter 30 minuten",
                10, 18, "🐟", "#B9D8CB", "salmon.jpg",
                [
                    (300m, "g", "Lachsfilet", "Fisch"),
                    (1m, "Stk.", "Brokkoli", "Obst & Gemüse"),
                    (150m, "g", "Jasminreis", "Nudeln & Reis"),
                    (1m, "EL", "helle Misopaste", "International"),
                    (1m, "EL", "Sojasauce", "International")
                ],
                [
                    "Reis nach Packungsangabe garen und den Backofen auf 210 °C vorheizen.",
                    "Misopaste mit Sojasauce und einem Esslöffel Wasser verrühren, den Lachs damit bestreichen.",
                    "Lachs und Brokkoli 14–16 Minuten backen und zusammen mit dem Reis anrichten."
                ]),
            Recipe(
                "Ofengemüse mit Feta",
                "Buntes Gemüse, goldener Feta und Kräuter – wenig Arbeit, viel Geschmack.",
                "vegetarisch, meal prep, ofengericht",
                12, 28, "🥕", "#F0C9A6", "vegetables.jpg",
                [
                    (1m, "Stk.", "Zucchini", "Obst & Gemüse"),
                    (2m, "Stk.", "Paprika", "Obst & Gemüse"),
                    (300m, "g", "kleine Kartoffeln", "Obst & Gemüse"),
                    (180m, "g", "Feta", "Kühlregal"),
                    (2m, "EL", "Olivenöl", "Vorrat")
                ],
                [
                    "Backofen auf 220 °C vorheizen und das Gemüse in mundgerechte Stücke schneiden.",
                    "Gemüse mit Olivenöl, Salz und Kräutern mischen und 20 Minuten rösten.",
                    "Feta darüberbröseln und weitere 8 Minuten goldbraun backen."
                ]),
            Recipe(
                "Cremige Tomaten-Gnocchi",
                "Samtige Tomatensauce, weiche Gnocchi und Basilikum für den schnellen Feierabend.",
                "schnell, vegetarisch, familienliebling",
                5, 15, "🍅", "#E6ADA0", "gnocchi.jpg",
                [
                    (500m, "g", "Gnocchi", "Kühlregal"),
                    (400m, "g", "gehackte Tomaten", "Konserven"),
                    (100m, "ml", "Kochsahne", "Kühlregal"),
                    (1m, "Bund", "Basilikum", "Obst & Gemüse"),
                    (1m, "Stk.", "Knoblauchzehe", "Obst & Gemüse")
                ],
                [
                    "Gnocchi in einer großen Pfanne rundherum goldbraun anbraten.",
                    "Knoblauch kurz mitbraten, Tomaten und Kochsahne zugeben und 8 Minuten köcheln.",
                    "Mit Basilikum, Salz und Pfeffer abschmecken."
                ]),
            Recipe(
                "Hähnchen-Souvlaki Bowl",
                "Würziges Hähnchen, Gurke, Tomaten und cremiger Joghurt auf lockerem Reis.",
                "proteinreich, bowl, familienliebling",
                15, 20, "🥙", "#E5D7B7", "souvlaki.jpg",
                [
                    (320m, "g", "Hähnchenbrust", "Fleisch"),
                    (150m, "g", "Langkornreis", "Nudeln & Reis"),
                    (0.5m, "Stk.", "Salatgurke", "Obst & Gemüse"),
                    (200m, "g", "Cherrytomaten", "Obst & Gemüse"),
                    (150m, "g", "griechischer Joghurt", "Kühlregal")
                ],
                [
                    "Reis garen. Hähnchen würfeln und mit Oregano, Paprika, Salz und Olivenöl würzen.",
                    "Hähnchen in einer heißen Pfanne 8–10 Minuten goldbraun braten.",
                    "Gemüse schneiden, Joghurt würzen und alles in Schalen anrichten."
                ]),
            Recipe(
                "Grünes Thai-Curry",
                "Aromatisches Kokoscurry mit viel grünem Gemüse und Limette.",
                "vegan, asiatisch, meal prep",
                12, 18, "🌿", "#BFD8AD", "curry.jpg",
                [
                    (400m, "ml", "Kokosmilch", "International"),
                    (1m, "EL", "grüne Currypaste", "International"),
                    (1m, "Stk.", "Zucchini", "Obst & Gemüse"),
                    (150m, "g", "Zuckerschoten", "Obst & Gemüse"),
                    (150m, "g", "Jasminreis", "Nudeln & Reis")
                ],
                [
                    "Reis nach Packungsangabe garen.",
                    "Currypaste kurz anrösten, Kokosmilch angießen und aufkochen.",
                    "Gemüse 8–10 Minuten im Curry garen und mit Limettensaft abschmecken."
                ])
        };

        foreach (var recipe in recipes)
        {
            var imagePath = Path.Combine(environment.WebRootPath, "images", "recipes", recipe.ImageFileName!);
            if (File.Exists(imagePath))
            {
                recipe.Entity.ImageData = await File.ReadAllBytesAsync(imagePath);
                recipe.Entity.ImageContentType = "image/jpeg";
            }

            db.Recipes.Add(recipe.Entity);
        }

        db.HouseholdSettings.Add(new HouseholdSettings
        {
            HouseholdName = "Unser Zuhause",
            DefaultServings = 2,
            WeeknightMaxMinutes = 35,
            WeekendMaxMinutes = 50,
            PlannedDinnersPerWeek = 5,
            PlannedLunchesPerWeek = 5,
            AvoidRepeatsWithinDays = 14,
            DietPreference = "Alles",
            PreferredTags = "schnell, meal prep",
            PantryStaples = "Salz, Pfeffer, Öl, Wasser, Gewürze"
        });
        await db.SaveChangesAsync();

        var weekStart = MealPlannerService.StartOfWeek(DateOnly.FromDateTime(DateTime.Now));
        var recipeIds = await db.Recipes.OrderBy(recipe => recipe.Id).Select(recipe => recipe.Id).ToListAsync();
        for (var day = 0; day < 5; day++)
        {
            db.MealPlanEntries.Add(new MealPlanEntry
            {
                Date = weekStart.AddDays(day),
                RecipeId = recipeIds[day % recipeIds.Count],
                Servings = 2
            });
        }

        await db.SaveChangesAsync();
    }

    private static SeedRecipe Recipe(
        string name,
        string description,
        string tags,
        int prepMinutes,
        int cookMinutes,
        string emoji,
        string accentColor,
        string imageFileName,
        IReadOnlyList<(decimal Quantity, string Unit, string Name, string Aisle)> ingredients,
        IReadOnlyList<string> steps)
    {
        var entity = new Recipe
        {
            Name = name,
            Description = description,
            Tags = tags,
            PrepMinutes = prepMinutes,
            CookMinutes = cookMinutes,
            Servings = 2,
            Emoji = emoji,
            AccentColor = accentColor
        };

        for (var index = 0; index < ingredients.Count; index++)
        {
            var ingredient = ingredients[index];
            entity.Ingredients.Add(new RecipeIngredient
            {
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
                Name = ingredient.Name,
                Aisle = ingredient.Aisle,
                SortOrder = index
            });
        }

        for (var index = 0; index < steps.Count; index++)
        {
            entity.Steps.Add(new RecipeStep
            {
                StepNumber = index + 1,
                Instruction = steps[index]
            });
        }

        return new SeedRecipe(entity, imageFileName);
    }

    private sealed record SeedRecipe(Recipe Entity, string? ImageFileName);
}
