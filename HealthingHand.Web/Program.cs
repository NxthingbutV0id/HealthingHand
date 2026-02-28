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

internal class Program // Personally, I prefer an explicit main method - Christian Torres
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("AppDb")));

        builder.Services.AddHealthingHandData(builder.Configuration.GetConnectionString("AppDb")!);

        // Add services to the container.
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
        });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddScoped<ISleepStore, SleepStore>();
        builder.Services.AddScoped<IDietStore, DietStore>();
        builder.Services.AddScoped<IWorkoutStore, WorkoutStore>();
        builder.Services.AddScoped<IAccountStore, AccountStore>();
        
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IWorkoutService, WorkoutService>();
        builder.Services.AddScoped<IDietService, DietService>();
        builder.Services.AddScoped<ISleepService, SleepService>();

        //builder.Services.AddScoped<UserSession>(); //TODO: Create UserSession class

        var app = builder.Build();
        
        // Apply migrations automatically (dev-friendly)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.MapAuthEndpoints();
        app.Run();
    }
}