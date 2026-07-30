using MealPrep.App.Services;
using Microsoft.AspNetCore.Http;

namespace MealPrep.App.Tests;

public sealed class FixedCredentialsOptionsTests
{
    [Fact]
    public void EmptyConfiguration_KeepsFixedCredentialsDisabled()
    {
        var options = new FixedCredentialsOptions();

        options.ValidateConfiguration();

        Assert.False(options.IsEnabled);
    }

    [Theory]
    [InlineData("mealprep", null)]
    [InlineData(null, "secret-password")]
    public void PartialConfiguration_IsRejected(string? username, string? password)
    {
        var options = new FixedCredentialsOptions
        {
            Username = username,
            Password = password
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateConfiguration);

        Assert.Contains("beide gesetzt", exception.Message);
    }

    [Fact]
    public void ConfiguredUsername_MustMatchExactly()
    {
        var options = new FixedCredentialsOptions
        {
            Username = "MealPrep",
            Password = "A-long-fixed-password!"
        };

        options.ValidateConfiguration();

        Assert.True(options.IsEnabled);
        Assert.True(options.MatchesUsername("MealPrep"));
        Assert.False(options.MatchesUsername("mealprep"));
        Assert.False(options.MatchesUsername("other"));
    }

    [Theory]
    [InlineData("/Account/Login", true)]
    [InlineData("/Account/Logout", true)]
    [InlineData("/Account/AccessDenied", true)]
    [InlineData("/Account/Register", false)]
    [InlineData("/Account/PasskeyRequestOptions", false)]
    [InlineData("/Account/Manage/Passkeys", false)]
    [InlineData("/settings", true)]
    public void AccountPolicy_OnlyAllowsRequiredFixedModeRoutes(
        string path,
        bool expected)
    {
        Assert.Equal(
            expected,
            FixedCredentialsAccessPolicy.IsAccountPathAllowed(new PathString(path)));
    }
}
