using MealPrep.App.Data;
using Microsoft.AspNetCore.Identity;

namespace MealPrep.App.Services;

public static class FixedCredentialsInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<FixedCredentialsOptions>();
        if (!options.IsEnabled)
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(FixedCredentialsInitializer));

        var username = options.Username!;
        var password = options.Password!;
        var user = await userManager.FindByNameAsync(username);

        if (user is null)
        {
            user = new ApplicationUser { UserName = username };
            EnsureSucceeded(
                await userManager.CreateAsync(user, password),
                "Das fest konfigurierte Konto konnte nicht angelegt werden");
        }
        else
        {
            var currentUsername = await userManager.GetUserNameAsync(user);
            if (!string.Equals(currentUsername, username, StringComparison.Ordinal))
            {
                EnsureSucceeded(
                    await userManager.SetUserNameAsync(user, username),
                    "Der Benutzername des fest konfigurierten Kontos konnte nicht aktualisiert werden");
            }

            if (!await userManager.CheckPasswordAsync(user, password))
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                EnsureSucceeded(
                    await userManager.ResetPasswordAsync(user, resetToken, password),
                    "Das Passwort des fest konfigurierten Kontos konnte nicht aktualisiert werden");
            }
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            EnsureSucceeded(
                await userManager.SetTwoFactorEnabledAsync(user, false),
                "Die Zwei-Faktor-Anmeldung konnte nicht deaktiviert werden");
        }

        foreach (var passkey in await userManager.GetPasskeysAsync(user))
        {
            EnsureSucceeded(
                await userManager.RemovePasskeyAsync(user, passkey.CredentialId),
                "Ein vorhandener Passkey konnte nicht entfernt werden");
        }

        foreach (var login in await userManager.GetLoginsAsync(user))
        {
            EnsureSucceeded(
                await userManager.RemoveLoginAsync(
                    user,
                    login.LoginProvider,
                    login.ProviderKey),
                "Eine vorhandene externe Anmeldung konnte nicht entfernt werden");
        }

        EnsureSucceeded(
            await userManager.UpdateSecurityStampAsync(user),
            "Bestehende Anmeldesitzungen konnten nicht ungültig gemacht werden");

        logger.LogInformation(
            "Fixed-Credentials-Modus für Benutzer {Username} aktiviert. " +
            "Registrierung und alternative Anmeldeverfahren sind deaktiviert.",
            username);
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"{message}: {errors}");
    }
}
