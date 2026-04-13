using System.Reflection;
using System.Security.Claims;
using HealthingHand.Data.Tests.Facades;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace HealthingHand.Data.Tests.Endpoints;

public static class TestAuthHelpers
{
    public static DefaultHttpContext CreateEndpointContext(
        Guid? userId = null,
        Dictionary<string, string>? formFields = null,
        FakeAuthenticationService? auth = null)
    {
        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            RequestServices = BuildEndpointServices(auth ?? new FakeAuthenticationService())
        };

        if (userId.HasValue)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    "TestAuth"));
        }

        if (formFields is null) return context;
        var form = new FormCollection(
            formFields.ToDictionary(
                kvp => kvp.Key,
                kvp => new StringValues(kvp.Value)));

        context.Features.Set<IFormFeature>(new FormFeature(form));

        return context;
    }

    public static ServiceProvider BuildEndpointServices(FakeAuthenticationService auth)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IAuthenticationService>(auth);

        return services.BuildServiceProvider();
    }
    
    public static async Task InvokePrivateStaticTaskMethod(Type declaringType, string methodName, params object[] args)
    {
        var method = declaringType.GetMethod(
                         methodName,
                         BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Could not find method '{methodName}'.");

        var task = method.Invoke(null, args) as Task
                   ?? throw new InvalidOperationException($"Method '{methodName}' did not return Task.");

        await task;
    }

    public static async Task<IResult> InvokePrivateStaticResultMethod(Type declaringType, string methodName, params object[] args)
    {
        var method = declaringType.GetMethod(
                         methodName,
                         BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Could not find method '{methodName}'.");

        var task = method.Invoke(null, args) as Task<IResult>
                   ?? throw new InvalidOperationException($"Method '{methodName}' did not return Task<IResult>.");

        return await task;
    }
}