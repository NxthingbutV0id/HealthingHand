using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace HealthingHand.Data.Tests;

public sealed class FakeAuthenticationService : IAuthenticationService
{
    public bool SignOutCalled { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
        => Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties? properties)
    {
        SignOutCalled = true;
        return Task.CompletedTask;
    }
}