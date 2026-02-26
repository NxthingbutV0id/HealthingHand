using System.Reflection;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Tests;

public sealed class UnitTest1 : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public UnitTest1()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.Migrate();
    }
    
    public void Dispose()
    {
        _connection.Dispose();
    }

    /// <summary>
    /// This test ensures that the Migrate() call in the constructor
    /// successfully creates the __EFMigrationsHistory table,
    /// which is essential for tracking applied migrations.
    /// </summary>
    [Fact]
    public void MigrateCreatesEfMigrationHistoryTable()
    {
        using var db = new AppDbContext(_options);
        
        var applied = db.Database.GetAppliedMigrations().ToList();
        
        Assert.NotEmpty(applied);
    }
    
    /// <summary>
    /// This test verifies that the DbContext's model contains entity types,
    /// which indicates that the model is properly configured
    /// and can be used for database operations.
    /// </summary>
    [Fact]
    public void DbContextModelContainsEntities()
    {
        using var db = new AppDbContext(_options);
        
        Assert.NotEmpty(db.Model.GetEntityTypes());
    }
    
    /// <summary>
    /// This test directly queries the SQLite master table
    /// to check for the existence of the __EFMigrationsHistory table,
    /// which is created by Entity Framework Core to track applied migrations.
    /// This ensures that the migration process has successfully set up
    /// the necessary infrastructure for managing database schema changes.
    /// </summary>
    [Fact]
    public void Sqlite_has_migrations_history_table()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        Assert.Contains("__EFMigrationsHistory", tables);
    }

    /// <summary>
    /// This test verifies that after creating a new user in the database,
    /// retrieving that user by their ID returns the same field values,
    /// ensuring that the data is correctly saved and retrieved from the database.
    /// </summary>
    [Fact]
    public void CreateUserThenGetByIdReturnsSameFields()
    {
        using var db = new AppDbContext(_options);

        var meta = GetUserMeta(db);

        var email = $"test_{Guid.NewGuid():N}@example.com";
        var display = "Test User";

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
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
    
    private sealed record UserMeta(Type UserClrType, string KeyPropName, string EmailPropName, string DisplayNamePropName);

    private static UserMeta GetUserMeta(AppDbContext db)
    {
        var entityTypes = db.Model.GetEntityTypes().ToList();
        
        var userEntity = entityTypes
            .FirstOrDefault(et =>
            {
                var clr = et.ClrType;
                var props = clr.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var hasEmail = props.Contains("Email") || props.Contains("EmailAddress");
                var hasKey = et.FindPrimaryKey() != null;
                return hasEmail && hasKey;
            });

        if (userEntity is null)
        {
            throw new InvalidOperationException(
                "Could not locate a User entity in the EF model. Ensure you have a mapped entity with an Email/EmailAddress property.");
        }
        
        var key = userEntity.FindPrimaryKey()!;
        if (key.Properties.Count != 1)
            throw new InvalidOperationException($"User entity primary key must be a single-column key for these tests. Found {key.Properties.Count} columns.");

        var keyPropName = key.Properties[0].Name;

        // Resolve Email property name
        var emailPropName = PickFirstExistingProperty(userEntity.ClrType, "Email", "EmailAddress")
                            ?? throw new InvalidOperationException("User entity does not contain an Email or EmailAddress property.");

        // Resolve display name property name (fallbacks)
        var displayNamePropName = PickFirstExistingProperty(userEntity.ClrType, "DisplayName", "Name", "Username")
                                  ?? throw new InvalidOperationException("User entity does not contain DisplayName/Name/Username property. Add one or update the test.");

        return new UserMeta(userEntity.ClrType, keyPropName, emailPropName, displayNamePropName);
    }
    
    private static object CreateUserInstance(UserMeta meta, string email, string displayName)
    {
        var user = Activator.CreateInstance(meta.UserClrType)
                   ?? throw new InvalidOperationException($"Could not create instance of {meta.UserClrType.FullName} (missing public parameterless constructor?).");

        // If key is Guid and currently empty, set it so we can reliably Find() later.
        var keyProp = meta.UserClrType.GetProperty(meta.KeyPropName, BindingFlags.Instance | BindingFlags.Public);
        if (keyProp is not null && keyProp.PropertyType == typeof(Guid))
        {
            var current = (Guid)(keyProp.GetValue(user) ?? Guid.Empty);
            if (current == Guid.Empty)
                keyProp.SetValue(user, Guid.NewGuid());
        }

        SetPropValue(user, meta.EmailPropName, email);
        SetPropValue(user, meta.DisplayNamePropName, displayName);

        return user;
    }

    private static string? PickFirstExistingProperty(Type t, params string[] candidates)
    {
        var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Select(p => p.Name)
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (from c in candidates
            where props.Contains(c)
            select t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .First(p => string.Equals(p.Name, c, StringComparison.OrdinalIgnoreCase))
                .Name).FirstOrDefault();
    }

    private static object? GetPropValue(object obj, string propName)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        return prop?.GetValue(obj);
    }

    private static void SetPropValue(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (prop is null)
            throw new InvalidOperationException($"Property '{propName}' not found on type '{obj.GetType().Name}'.");

        prop.SetValue(obj, value);
    }
}
