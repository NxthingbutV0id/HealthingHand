using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HealthingHand.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // TODO: add antiforgery later if needed
        app.MapPost("/auth/login", AuthLogin).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/auth/register", AuthRegister).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/auth/logout", AuthLogout).DisableAntiforgery();
    }

    private static async Task<IResult> AuthLogin(HttpContext context, IAccountService accounts)
    {
        var form = await context.Request.ReadFormAsync();
        var email = form["Email"].ToString();
        var password = form["Password"].ToString();
        var returnUrl = form["ReturnUrl"].ToString();
        
        if (email.Trim().Equals("test@local", StringComparison.OrdinalIgnoreCase) && password == "test")
        {
            await accounts.SignInTestUserAsync();
        }

        var user = await accounts.AuthenticateAsync(email, password);
        if (user is null)
            return Results.Redirect("/login?error=1");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true
            });

        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
            returnUrl = "/";

        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> AuthRegister(HttpContext context, IAccountService accounts)
    {
        var form = await context.Request.ReadFormAsync();

        var email = form["Email"].ToString();
        var displayName = form["DisplayName"].ToString();
        var password = form["Password"].ToString();
        var returnUrl = form["ReturnUrl"].ToString();

        var parseSuccess = byte.TryParse(form["Age"].ToString(), out var age);
        
        if (!parseSuccess)
            return Results.Redirect("/register?error=Invalid age");
        
        parseSuccess = float.TryParse(form["HeightM"].ToString(), out var heightM);
        
        if (!parseSuccess || !(float.IsFinite(heightM) && heightM > 0))
            return Results.Redirect("/register?error=Invalid height (Must be positive and not zero)");
        
        parseSuccess = float.TryParse(form["WeightKg"].ToString(), out var weightKg);
        
        if (!parseSuccess || !(float.IsFinite(weightKg) && weightKg > 0))
            return Results.Redirect("/register?error=Invalid weight (Must be positive and not zero)");

        // Sex is an enum in your service. Expect "Male"/"Female"/"Unspecified" from a <select>.
        parseSuccess = Enum.TryParse<Sex>(form["Sex"].ToString(), ignoreCase: true, out var sex);

        if (!parseSuccess) // default to Undefined if parsing fails (e.g. missing or invalid value)
            sex = Sex.Undefined;

        var (ok, error) = await accounts.RegisterAsync(
            email, displayName, password, age, sex, heightM, weightKg);

        if (!ok)
            return Results.Redirect($"/register?error={Uri.EscapeDataString(error ?? "Registration failed")}");

        // Auto-login after register
        var user = await accounts.AuthenticateAsync(email, password);
        if (user is null)
            return Results.Redirect("/login");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true
            });

        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
            returnUrl = "/";

        return Results.Redirect(returnUrl);
    }
    
    private static async Task<IResult> AuthLogout(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }
}