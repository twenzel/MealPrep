using System.Reflection;

namespace MealPrep.App.Services;

public static class ApplicationVersion
{
    private static readonly Assembly AppAssembly = typeof(ApplicationVersion).Assembly;

    public static string Current { get; } = ToDisplayVersion(
        AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        AppAssembly.GetName().Version);

    public static string ToDisplayVersion(
        string? informationalVersion,
        Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var value = informationalVersion.Trim();
            var metadataSeparator = value.IndexOf('+');
            return metadataSeparator > 0 ? value[..metadataSeparator] : value;
        }

        return assemblyVersion?.ToString(3) ?? "Unknown";
    }
}
