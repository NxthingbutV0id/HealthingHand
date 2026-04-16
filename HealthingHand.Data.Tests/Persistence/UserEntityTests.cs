using System.Reflection;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using static HealthingHand.Data.Tests.Infrastructure.EfModelMetadataHelper;
using static HealthingHand.Data.Tests.Infrastructure.ReflectionHelpers;

namespace HealthingHand.Data.Tests.Persistence;

public class UserEntityTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    /// <summary>
    /// This test verifies that after creating a new user in the database,
    /// retrieving that user by their ID returns the same field values,
    /// ensuring that the data is correctly saved and retrieved from the database.
    /// </summary>
    [Fact]
    public void CreateUserThenGetByIdReturnsSameFields()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"test_{Guid.NewGuid():N}@example.com";
        const string display = "Test User";

        var user = CreateUserInstance(meta, email, display);

        db.Add(user);
        db.SaveChanges();

        var keyValue = GetPropValue(user, meta.KeyPropName)
                       ?? throw new InvalidOperationException("User key value was null after SaveChanges().");

        db.ChangeTracker.Clear();

        var fetched = db.Find(meta.UserClrType, keyValue)
                      ?? throw new InvalidOperationException("Could not find user by primary key after insertion.");

        Assert.Equal(email, GetPropValue(fetched, meta.EmailPropName) as string);
        Assert.Equal(display, GetPropValue(fetched, meta.DisplayNamePropName) as string);
    }

    /// <summary>
    /// This test checks that after creating a new user in the database,
    /// retrieving that user by their email address returns the same field values,
    /// confirming that the email-based retrieval functionality works correctly
    /// and that the data is accurately stored and accessible via the email field.
    /// </summary>
    [Fact]
    public void CreateUserThenGetByEmailReturnsUser()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"test_{Guid.NewGuid():N}@example.com";
        const string display = "Email Lookup User";

        var user = CreateUserInstance(meta, email, display);

        db.Add(user);
        db.SaveChanges();

        db.ChangeTracker.Clear();

        var found = db.Users.SingleOrDefault(u => u.Email == email);

        Assert.NotNull(found);
        Assert.Equal(email, GetPropValue(found, meta.EmailPropName) as string);
        Assert.Equal(display, GetPropValue(found, meta.DisplayNamePropName) as string);
    }

    /// <summary>
    /// This test ensures that the database enforces a unique constraint on the email field of the User entity.
    /// </summary>
    [Fact]
    public void CreatingTwoUsersWithSameEmailThrowsException()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"dupe_{Guid.NewGuid():N}@example.com";

        var user1 = CreateUserInstance(meta, email, "Dupe A");
        var user2 = CreateUserInstance(meta, email, "Dupe B");

        db.Add(user1);
        db.Add(user2);

        // If you have a UNIQUE constraint/index on Email, SQLite will throw and EF wraps as DbUpdateException.
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    /// <summary>
    /// This test that the data in the database is updated and persists correctly.
    /// </summary>
    [Fact]
    public void UpdateDisplayNamePersists()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"update_{Guid.NewGuid():N}@example.com";
        const string original = "Original Name";
        const string updated = "Updated Name";

        var user = CreateUserInstance(meta, email, original);

        db.Add(user);
        db.SaveChanges();

        var keyValue = GetPropValue(user, meta.KeyPropName)
                       ?? throw new InvalidOperationException("User key value was null after SaveChanges().");

        // Update (tracked)
        SetPropValue(user, meta.DisplayNamePropName, updated);
        db.SaveChanges();

        // Verify with a fresh context/query (ensures it persisted, not just tracked memory)
        db.ChangeTracker.Clear();

        var fetched = db.Find(meta.UserClrType, keyValue)
                      ?? throw new InvalidOperationException("Could not re-load user by key after update.");

        Assert.Equal(updated, GetPropValue(fetched, meta.DisplayNamePropName) as string);
    }

    /// <summary>
    /// CRUD test for User deletion.
    /// </summary>
    [Fact]
    public void DeleteUserRemovesRow()
    {
        using var db = fixture.CreateDb();

        var (userMeta, user, userKey) = CreateAndSaveUser(db, "delete");

        // Delete
        db.Remove(user);
        db.SaveChanges();

        db.ChangeTracker.Clear();

        var fetched = db.Find(userMeta.UserClrType, userKey);
        Assert.Null(fetched);
    }

    
    /// <summary>
    /// This test is to check the login functionality by creating a user with a known password
    /// then retrieving that user and verifying the password.
    /// </summary>
    [Fact]
    public void UserLoginTestValid()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"login_{Guid.NewGuid():N}@example.com";
        const string password = "CorrectPassword123!";
        const string display = "Login User";

        var user = CreateUserInstance(meta, email, display);
        SetUserPassword(user, meta, password);

        db.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var found = db.Users.SingleOrDefault(u => u.Email == email);

        Assert.NotNull(found);
        Assert.True(VerifyUserPassword(found, meta, password));
    }
    
    /// <summary>
    /// This test is the same as UserLoginTestValid with the difference that
    /// it verifies that an incorrect password does not validate successfully
    /// </summary>
    [Fact]
    public void UserLoginTestInvalid()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"badlogin_{Guid.NewGuid():N}@example.com";
        const string correctPassword = "CorrectPassword123!";
        const string wrongPassword = "WrongPassword123!";
        const string display = "Invalid Login User";

        var user = CreateUserInstance(meta, email, display);
        SetUserPassword(user, meta, correctPassword);

        db.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var found = db.Users.SingleOrDefault(u => u.Email == email);

        Assert.NotNull(found);
        Assert.False(VerifyUserPassword(found, meta, wrongPassword));
    }
    
    /// <summary>
    /// This test checks for registering a new user with a unique email
    /// and ensures that the user is created successfully with the correct field values,
    /// and that the password is stored (hashed if applicable) in the database.
    /// </summary>
    [Fact]
    public void UserRegisterTestValid()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"register_{Guid.NewGuid():N}@example.com";
        const string password = "RegisterPassword123!";
        const string display = "Registered User";

        var user = CreateUserInstance(meta, email, display);
        SetUserPassword(user, meta, password);

        db.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var found = db.Users.SingleOrDefault(u => u.Email == email);

        Assert.NotNull(found);
        Assert.Equal(email, GetPropValue(found, meta.EmailPropName) as string);
        Assert.Equal(display, GetPropValue(found, meta.DisplayNamePropName) as string);

        if (string.IsNullOrWhiteSpace(meta.PasswordPropName)) return;
        var storedPassword = GetPropValue(found, meta.PasswordPropName!) as string;
        Assert.False(string.IsNullOrWhiteSpace(storedPassword));
    }
    
    /// <summary>
    /// This test is similar to the UserRegisterTestValid() but attempts to create two users with the same email address
    /// </summary>
    [Fact]
    public void UserRegisterTestInvalid()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"dupe_register_{Guid.NewGuid():N}@example.com";

        var user1 = CreateUserInstance(meta, email, "User One");
        var user2 = CreateUserInstance(meta, email, "User Two");

        SetUserPassword(user1, meta, "Password123!");
        SetUserPassword(user2, meta, "AnotherPassword123!");

        db.Add(user1);
        db.Add(user2);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
    
    /// <summary>
    /// This test is to verify that deleting a user from the database works correctly, and that after deletion,
    /// the user is unable to login
    /// </summary>
    [Fact]
    public void UserDeletionTest()
    {
        using var db = fixture.CreateDb();

        var meta = GetUserMeta(db);

        var email = $"delete_login_{Guid.NewGuid():N}@example.com";
        const string password = "DeleteMe123!";
        const string display = "Delete Me";

        var user = CreateUserInstance(meta, email, display);
        SetUserPassword(user, meta, password);

        db.Add(user);
        db.SaveChanges();

        db.Remove(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var found = db.Users.SingleOrDefault(u => u.Email == email);

        Assert.Null(found);
    }

    private static bool VerifyUserPassword(object user, UserMeta meta, string rawPassword)
    {
        if (string.IsNullOrWhiteSpace(meta.PasswordPropName))
            throw new InvalidOperationException(
                "No Password/PasswordHash property was found on the User entity, so login tests cannot verify credentials.");

        var stored = GetPropValue(user, meta.PasswordPropName!) as string;

        if (string.IsNullOrWhiteSpace(stored)) return false;

        // Plaintext fallback
        if (stored == rawPassword) return true;

        // Identity hash fallback
        var hasher = new PasswordHasher<object>();
        var result = hasher.VerifyHashedPassword(user, stored, rawPassword);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}