using HealthingHand.Data.Tests.Facades;
using HealthingHand.Web.Endpoints;
using Microsoft.AspNetCore.Http;

using static HealthingHand.Data.Tests.Endpoints.TestAuthHelpers;

namespace HealthingHand.Data.Tests.Endpoints;

public class AuthEndpointTests
{
    [Fact]
    public async Task AuthLogout_Endpoint_RedirectsToLogout()
    {
        var auth = new FakeAuthenticationService();
        var context = CreateEndpointContext(auth: auth);

        await InvokePrivateStaticTaskMethod(typeof(AuthEndpoints), "AuthLogout", context);

        Assert.True(auth.SignOutCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/logout", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task AuthDeleteAccount_Endpoint_Success_RedirectsToLoginDeleted()
    {
        var userId = Guid.NewGuid();
        var auth = new FakeAuthenticationService();
        var accounts = new StubAccountService
        {
            DeleteAccountResponse = (true, null)
        };

        var context = CreateEndpointContext(
            userId,
            new Dictionary<string, string>
            {
                ["CurrentPassword"] = "CorrectPassword123!",
                ["Confirmation"] = "DELETE"
            },
            auth);

        var result = await InvokePrivateStaticResultMethod(
            typeof(AuthEndpoints),
            "AuthDeleteAccount",
            context,
            accounts);

        await result.ExecuteAsync(context);

        Assert.True(accounts.DeleteAccountCalled);
        Assert.Equal(userId, accounts.DeleteAccountUserId);
        Assert.Equal("CorrectPassword123!", accounts.DeleteAccountPassword);

        Assert.True(auth.SignOutCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/login?deleted=1", context.Response.Headers.Location.ToString());
    }
}