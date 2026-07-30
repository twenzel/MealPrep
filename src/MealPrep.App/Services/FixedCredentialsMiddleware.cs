using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace MealPrep.App.Services;

public sealed class FixedCredentialsMiddleware(
    RequestDelegate next,
    FixedCredentialsOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!options.IsEnabled)
        {
            await next(context);
            return;
        }

        if (!FixedCredentialsAccessPolicy.IsAccountPathAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true &&
            !options.MatchesUsername(context.User.Identity.Name))
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());

            if (FixedCredentialsAccessPolicy.IsLoginPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var returnUrl =
                $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(
                QueryHelpers.AddQueryString("/Account/Login", "ReturnUrl", returnUrl));
            return;
        }

        await next(context);
    }
}
