using Microsoft.AspNetCore.Http;

namespace MealPrep.App.Services;

public sealed class FixedCredentialsOptions
{
    public const string SectionName = "Authentication:FixedCredentials";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrEmpty(Password);

    public void ValidateConfiguration()
    {
        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasPassword = !string.IsNullOrEmpty(Password);

        if (hasUsername != hasPassword)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Username und {SectionName}:Password müssen " +
                "entweder beide gesetzt oder beide leer sein.");
        }

        if (hasUsername &&
            !string.Equals(Username, Username!.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Username darf keine führenden oder nachfolgenden Leerzeichen enthalten.");
        }
    }

    public bool MatchesUsername(string? username) =>
        IsEnabled &&
        string.Equals(Username, username, StringComparison.Ordinal);
}

public static class FixedCredentialsAccessPolicy
{
    public static bool IsAccountPathAllowed(PathString path)
    {
        if (!path.StartsWithSegments("/Account", out var remaining))
        {
            return true;
        }

        var accountPath = remaining.Value?.TrimEnd('/');
        return string.Equals(accountPath, "/Login", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(accountPath, "/Logout", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(accountPath, "/AccessDenied", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLoginPath(PathString path)
    {
        if (!path.StartsWithSegments("/Account", out var remaining))
        {
            return false;
        }

        return string.Equals(
            remaining.Value?.TrimEnd('/'),
            "/Login",
            StringComparison.OrdinalIgnoreCase);
    }
}
