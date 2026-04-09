using HealthingHand.Data;
using HealthingHand.Data.Persistence;
using HealthingHand.Web.Components;
using HealthingHand.Web.Endpoints;
using HealthingHand.Web.Services;
using HealthingHand.Web.Services.OcrItems;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Web;

internal class Program // Personally, I prefer an explicit main method - CT
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "app.db");
        var cs = $"Data Source={dbPath}";

        // Data layer (factory + stores + IDatabase)
        builder.Services.AddHealthingHandData(cs);

        // Web / UI
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/auth/logout";
                options.AccessDeniedPath = "/login";
                
                // Default Time is 10 minutes.
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);

                // Turn this OFF while testing so the timeout is easier to observe
                options.SlidingExpiration = true;

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = context =>
                    {
                        Console.WriteLine(
                            $"[AUTH] ValidatePrincipal | now={DateTimeOffset.UtcNow:u} | " +
                            $"issued={context.Properties.IssuedUtc:u} | " +
                            $"expires={context.Properties.ExpiresUtc:u} | " +
                            $"user={context.Principal?.Identity?.Name ?? "(none)"}");

                        return Task.CompletedTask;
                    },
                    OnRedirectToLogin = context =>
                    {
                        Console.WriteLine(
                            $"[AUTH] RedirectToLogin | path={context.Request.Path} | redirect={context.RedirectUri}");

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();

        // App services
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IWorkoutService, WorkoutService>();
        builder.Services.AddScoped<IDietService, DietService>();
        builder.Services.AddScoped<ISleepService, SleepService>();
        builder.Services.AddScoped<IWeightService, WeightService>();
        builder.Services.AddScoped<INutritionLabelOcrService, NutritionLabelOcrService>();
        builder.Services.AddScoped<INutritionLabelParser, NutritionLabelParser>();

        var app = builder.Build();

        // Auto-migrate
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();

            Console.WriteLine($"[DB] SQLite DataSource = {db.Database.GetDbConnection().DataSource}");

            db.Database.Migrate();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.MapAuthEndpoints();
        
        app.MapGet("/auth/session-debug", async (HttpContext context) =>
        {
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var isAuthenticated =
                result is { Succeeded: true, Principal.Identity.IsAuthenticated: true };

            var expiresUtc = result.Properties?.ExpiresUtc;
            var issuedUtc = result.Properties?.IssuedUtc;

            double? minutesRemaining = null;
            if (expiresUtc.HasValue)
            {
                minutesRemaining = Math.Round((expiresUtc.Value - DateTimeOffset.UtcNow).TotalMinutes, 2);
            }

            return Results.Json(new
            {
                UtcNow = DateTimeOffset.UtcNow,
                IsAuthenticated = isAuthenticated,
                Name = result.Principal?.Identity?.Name,
                IssuedUtc = issuedUtc,
                ExpiresUtc = expiresUtc,
                MinutesRemaining = minutesRemaining,
                IsPersistent = result.Properties?.IsPersistent ?? false
            });
        }).AllowAnonymous();
        
        app.Run();
    }
}