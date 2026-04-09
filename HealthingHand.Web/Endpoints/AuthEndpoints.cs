using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace HealthingHand.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // TODO: add antiforgery later if needed
        app.MapPost("/auth/login", AuthLogin).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/auth/register", AuthRegister).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/auth/logout", AuthLogout).DisableAntiforgery();
        app.MapPost("/auth/delete-account", AuthDeleteAccount).DisableAntiforgery();
        app.MapPost("/auth/update-display-name", AuthUpdateDisplayName).DisableAntiforgery();
    }

    private static async Task<IResult> AuthLogin(
        HttpContext context,
        IAccountService accounts,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        var opts = cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Console.WriteLine(
            $"[AUTH] Login config | ExpireTimeSpan={opts.ExpireTimeSpan} | " +
            $"SlidingExpiration={opts.SlidingExpiration}");

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

        var issued = DateTimeOffset.UtcNow;
        var expires = issued.Add(opts.ExpireTimeSpan);

        Console.WriteLine(
            $"[AUTH] Issuing cookie | issued={issued:u} | expires={expires:u}");

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                IssuedUtc = issued,
                ExpiresUtc = expires,
                AllowRefresh = true
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

        // Sex is an enum in your service. Expect "Male"/"Female"/"Unspecified" from a <select>.
        parseSuccess = Enum.TryParse<Sex>(form["Sex"].ToString(), ignoreCase: true, out var sex);

        if (!parseSuccess) // default to Undefined if parsing fails (e.g. missing or invalid value)
            sex = Sex.Undefined;

        var (ok, error) = await accounts.RegisterAsync(
            email, displayName, password, age, sex, heightM);

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
            new(ClaimTypes.Email, user.Email)
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
    
    private static async Task AuthLogout(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/logout");
    }
    
    private static async Task SignInUserAsync(HttpContext context, UserEntry user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
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
    }

    private static async Task<IResult> AuthUpdateDisplayName(HttpContext context, IAccountService accounts)
    {
        var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Redirect("/login");

        var form = await context.Request.ReadFormAsync();
        var displayName = form["DisplayName"].ToString();

        var (ok, error, user) = await accounts.UpdateDisplayNameAsync(userId, displayName);
        if (!ok || user is null)
            return Results.Redirect($"/user?error={Uri.EscapeDataString(error ?? "Profile update failed")}");

        await SignInUserAsync(context, user);
        return Results.Redirect("/user?success=Display%20name%20updated");
    }

    private static async Task<IResult> AuthDeleteAccount(HttpContext context, IAccountService accounts)
    {
        var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.Redirect("/login");

        var form = await context.Request.ReadFormAsync();
        var currentPassword = form["CurrentPassword"].ToString();
        var confirmation = form["Confirmation"].ToString();

        if (!string.Equals(confirmation, "DELETE", StringComparison.Ordinal))
            return Results.Redirect("/user?deleteError=Type%20DELETE%20to%20confirm");

        var (ok, error) = await accounts.DeleteAccountAsync(userId, currentPassword);
        if (!ok)
            return Results.Redirect($"/user?deleteError={Uri.EscapeDataString(error ?? "Delete failed")}");

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login?deleted=1");
    }
}

