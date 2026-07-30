using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using MealPrep.App.Components;
using MealPrep.App.Components.Account;
using MealPrep.App.Data;
using MealPrep.App.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var germanCulture = CultureInfo.GetCultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = germanCulture;
CultureInfo.DefaultThreadCurrentUICulture = germanCulture;

var builder = WebApplication.CreateBuilder(args);

var fixedCredentials = builder.Configuration
    .GetSection(FixedCredentialsOptions.SectionName)
    .Get<FixedCredentialsOptions>() ?? new();
fixedCredentials.ValidateConfiguration();
builder.Services.AddSingleton(fixedCredentials);
if (fixedCredentials.IsEnabled)
{
    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.Zero);
}

var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddResponseCompression();
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<MealPlannerService>();
builder.Services.AddScoped<InstagramRecipeImportService>();
builder.Services.AddHttpClient("instagram-import", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(12);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mahlzeit/1.0 (+self-hosted recipe importer)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.Brotli |
                                 DecompressionMethods.GZip |
                                 DecompressionMethods.Deflate
    });

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        if (!fixedCredentials.IsEnabled)
        {
            options.Password.RequiredLength = 10;
        }
        else
        {
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
        }
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseAuthentication();
app.UseMiddleware<FixedCredentialsMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/recipes/{id:int}/image", async (
    int id,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    HttpContext httpContext) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var image = await db.Recipes
        .Where(recipe => recipe.Id == id && recipe.ImageData != null)
        .Select(recipe => new { recipe.ImageData, recipe.ImageContentType })
        .SingleOrDefaultAsync();

    if (image?.ImageData is null)
    {
        return Results.NotFound();
    }

    var etag = $"\"{Convert.ToHexString(SHA256.HashData(image.ImageData))}\"";
    if (httpContext.Request.Headers.IfNoneMatch == etag)
    {
        return Results.StatusCode(StatusCodes.Status304NotModified);
    }

    httpContext.Response.Headers.ETag = etag;
    httpContext.Response.Headers.CacheControl = "private,max-age=0,must-revalidate";
    return Results.Bytes(image.ImageData, image.ImageContentType ?? "image/webp");
}).RequireAuthorization();

await SeedData.InitializeAsync(app.Services, app.Environment);
await FixedCredentialsInitializer.InitializeAsync(app.Services);

await app.RunAsync();
