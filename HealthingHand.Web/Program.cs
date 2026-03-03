using HealthingHand.Data;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Components;
using HealthingHand.Web.Endpoints;
using HealthingHand.Web.Services;
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

        // After logging out, the user is stuck on a blank page until refresh
        // TODO: figure out why and fix it
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.LoginPath = "/login";
        });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();

        // App services
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IWorkoutService, WorkoutService>();
        builder.Services.AddScoped<IDietService, DietService>();
        builder.Services.AddScoped<ISleepService, SleepService>();

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
        app.Run();
    }
}