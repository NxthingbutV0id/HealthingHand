using HealthingHand.Data.Entries;
using System.Security.Cryptography;
using HealthingHand.Data.Stores;

namespace HealthingHand.Web.Services;

public interface IAccountService
{
    UserEntry? CurrentUser { get; }

    bool IsSignedIn { get; }

    event Action? AuthStateChanged;

    Task<UserEntry> SignInTestUserAsync();

    Task<(bool Success, string? Error)> RegisterAsync(
        string email,
        string displayName,
        string password,
        byte age,
        Sex sex,
        float heightM,
        float weightKg);

    Task<(bool Success, string? Error)> SignInAsync(string email, string password);

    Task SignOutAsync();

    Task RefreshCurrentUserAsync();

    Task<(bool Success, string? Error)> UpdateProfileAsync(
        string displayName,
        byte age,
        Sex sex,
        float heightM,
        float weightKg);

    Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword);
    
    Task<UserEntry?> AuthenticateAsync(string email, string password);
    
    Task<(bool Success, string? Error)> DeleteCurrentAccountAsync(Guid userId, string currentPassword);
}

public class AccountService(IAccountStore accounts) : IAccountService
{
    public UserEntry? CurrentUser { get; private set; }
    public bool IsSignedIn => CurrentUser is not null;
    public event Action? AuthStateChanged;

    public async Task<UserEntry> SignInTestUserAsync()
    {
        const string testEmail = "test@local";
        const string testPassword = "test";

        var email = NormalizeEmail(testEmail);
        var existing = await accounts.GetByEmailAsync(email);

        if (existing is null)
        {
            existing = new UserEntry
            {
                Email = email,
                DisplayName = "TestUser",
                PasswordHash = HashPassword(testPassword),
                Age = 20,
                Sex = Sex.Male,
                HeightM = 1.75f,
                WeightKg = 70f,
                CreationDate = DateTime.UtcNow,
                LastOnline = DateTime.UtcNow
            };

            await accounts.AddAsync(existing);
        }

        existing.LastOnline = DateTime.UtcNow;
        await accounts.UpdateAsync(existing);

        CurrentUser = existing;
        AuthStateChanged?.Invoke();
        return existing;
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(
        string email,
        string displayName,
        string password,
        byte age,
        Sex sex,
        float heightM,
        float weightKg)
    {
        email = NormalizeEmail(email);
        displayName = displayName.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required.");
        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name is required.");
        if (!IsPasswordStrongEnough(password))
            return (false, "Password must be at least 8 characters.");
        if (heightM <= 0 || weightKg <= 0)
            return (false, "Height and weight must be positive.");

        var existing = await accounts.GetByEmailAsync(email);
        if (existing is not null)
            return (false, "An account with that email already exists.");

        var user = new UserEntry
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = HashPassword(password),
            Age = age,
            Sex = sex,
            HeightM = heightM,
            WeightKg = weightKg,
            CreationDate = DateTime.UtcNow,
            LastOnline = DateTime.UtcNow
        };

        await accounts.AddAsync(user);

        // Optional: auto-sign-in after register
        CurrentUser = user;
        AuthStateChanged?.Invoke();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
    {
        email = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        var user = await accounts.GetByEmailAsync(email);
        if (user is null || !VerifyPassword(user.PasswordHash, password))
            return (false, "Invalid email or password.");

        user.LastOnline = DateTime.UtcNow;
        await accounts.UpdateAsync(user);

        CurrentUser = user;
        AuthStateChanged?.Invoke();
        return (true, null);
    }

    public Task SignOutAsync()
    {
        CurrentUser = null;
        AuthStateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async Task RefreshCurrentUserAsync()
    {
        if (CurrentUser is null) return;

        var refreshed = await accounts.GetAsync(CurrentUser.Id);
        CurrentUser = refreshed; // may become null if deleted
        AuthStateChanged?.Invoke();
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(
        string displayName,
        byte age,
        Sex sex,
        float heightM,
        float weightKg)
    {
        if (CurrentUser is null)
            return (false, "Not signed in.");

        displayName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name is required.");
        if (heightM <= 0 || weightKg <= 0)
            return (false, "Height and weight must be positive.");

        CurrentUser.DisplayName = displayName;
        CurrentUser.Age = age;
        CurrentUser.Sex = sex;
        CurrentUser.HeightM = heightM;
        CurrentUser.WeightKg = weightKg;

        await accounts.UpdateAsync(CurrentUser);
        AuthStateChanged?.Invoke();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (CurrentUser is null)
            return (false, "Not signed in.");

        if (!VerifyPassword(CurrentUser.PasswordHash, currentPassword))
            return (false, "Current password is incorrect.");

        if (!IsPasswordStrongEnough(newPassword))
            return (false, "New password must be at least 8 characters.");

        CurrentUser.PasswordHash = HashPassword(newPassword);
        await accounts.UpdateAsync(CurrentUser);

        return (true, null);
    }
    
    public async Task<UserEntry?> AuthenticateAsync(string email, string password)
    {
        email = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await accounts.GetByEmailAsync(email);
        if (user is null) return null;

        if (!VerifyPassword(user.PasswordHash, password))
            return null;

        user.LastOnline = DateTime.UtcNow;

        await accounts.UpdateAsync(user);
        return user;
    }
    
    public async Task<(bool Success, string? Error)> DeleteCurrentAccountAsync(Guid userId, string currentPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            return (false, "Password is required.");
    
        var user = await accounts.GetAsync(userId);
        if (user is null)
            return (false, "Account not found.");
    
        if (!VerifyPassword(user.PasswordHash, currentPassword))
            return (false, "Current password is incorrect.");
    
        await accounts.DeleteAsync(userId);

        if (CurrentUser?.Id != userId) return (true, null);
        CurrentUser = null;
        AuthStateChanged?.Invoke();

        return (true, null);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static bool IsPasswordStrongEnough(string password) => !string.IsNullOrEmpty(password) && password.Length >= 8;

    // Format: pbkdf2-sha256$<iters>$<saltB64>$<keyB64>
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    private static bool VerifyPassword(string stored, string password)
    {
        if (string.IsNullOrWhiteSpace(stored)) return false;

        var parts = stored.Split('$');
        if (parts.Length != 4) return false;
        if (!string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal)) return false;
        if (!int.TryParse(parts[1], out var iters)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: iters,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}