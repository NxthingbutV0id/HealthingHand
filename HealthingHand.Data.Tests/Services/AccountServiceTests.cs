using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Data.Tests.Infrastructure;
using HealthingHand.Web.Services;

namespace HealthingHand.Data.Tests.Services;

public class AccountServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task Login_ValidCredentials_SetsCurrentUser()
    {
        var service = CreateAccountService();

        var email = $"auth_login_{Guid.NewGuid():N}@example.com";

        var register = await service.RegisterAsync(
            email,
            "Auth User",
            "CorrectPassword123!",
            20,
            Sex.Male,
            1.80f);

        Assert.True(register.Success);

        await service.SignOutAsync();

        var login = await service.SignInAsync(email, "CorrectPassword123!");

        Assert.True(login.Success);
        Assert.Null(login.Error);
        Assert.True(service.IsSignedIn);
        Assert.NotNull(service.CurrentUser);
        Assert.Equal(email, service.CurrentUser!.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsFriendlyError()
    {
        var service = CreateAccountService();

        var first = await service.RegisterAsync(
            "  DUPETEST@example.com  ",
            "User One",
            "Password123!",
            20,
            Sex.Male,
            1.80f);

        var second = await service.RegisterAsync(
            "dupetest@example.com",
            "User Two",
            "Password123!",
            20,
            Sex.Male,
            1.80f);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("An account with that email already exists.", second.Error);
    }
    
    private AccountService CreateAccountService()
    {
        return new AccountService(new AccountStore(fixture.Factory));
    }
}