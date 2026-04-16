using HealthingHand.Data.Entries;
using HealthingHand.Web.Services;

namespace HealthingHand.Data.Tests;

public sealed class StubAccountService : IAccountService
{
    public UserEntry? CurrentUser => null;
    public bool IsSignedIn => false;
    public event Action? AuthStateChanged;

    public bool DeleteAccountCalled { get; private set; }
    public Guid DeleteAccountUserId { get; private set; }
    public string? DeleteAccountPassword { get; private set; }
    public (bool Success, string? Error) DeleteAccountResponse { get; set; } = (true, null);

    public Task<UserEntry> SignInTestUserAsync() => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> RegisterAsync(string email, string displayName, string password, byte age, Sex sex, float heightM) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> SignInAsync(string email, string password) => throw new NotSupportedException();
    public Task SignOutAsync() => Task.CompletedTask;
    public Task RefreshCurrentUserAsync() => Task.CompletedTask;
    public Task<(bool Success, string? Error)> UpdateProfileAsync(string displayName, byte age, Sex sex, float heightM) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword) => throw new NotSupportedException();
    public Task<UserEntry?> AuthenticateAsync(string email, string password) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> DeleteCurrentAccountAsync(Guid userId, string currentPassword) => throw new NotSupportedException();
    public Task<UserEntry?> GetByIdAsync(Guid userId) => throw new NotSupportedException();
    public Task<(bool Success, string? Error, UserEntry? User)> UpdateDisplayNameAsync(Guid userId, string displayName) => throw new NotSupportedException();

    public Task<(bool Success, string? Error)> DeleteAccountAsync(Guid userId, string currentPassword)
    {
        DeleteAccountCalled = true;
        DeleteAccountUserId = userId;
        DeleteAccountPassword = currentPassword;
        return Task.FromResult(DeleteAccountResponse);
    }
}