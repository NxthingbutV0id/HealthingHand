using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace HealthingHand.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (HttpContext context) =>
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "TestUser")
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            return Results.Redirect("/");
        });

        app.MapPost("/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.Redirect("/");
        });
    }
}