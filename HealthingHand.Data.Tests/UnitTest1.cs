using System.Reflection;
using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Endpoints;
using HealthingHand.Web.Services;
using HealthingHand.Web.Services.DietItems;
using HealthingHand.Web.Services.OcrItems;
using HealthingHand.Web.Services.SleepItems;
using HealthingHand.Web.Services.WorkoutItems;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace HealthingHand.Data.Tests;

public sealed class UnitTest1 : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UnitTest1()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.Migrate();
        
        _factory = new TestDbContextFactory(_options);
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
    /// This test is used to check that the database migrations are applied as expected.
    /// </summary>
    [Fact]
    public void Debug_ListAppliedMigrations_And_UserColumns()
    {
        using var db = new AppDbContext(_options);

        var allMigrations = db.Database.GetMigrations().ToList();
        var appliedMigrations = db.Database.GetAppliedMigrations().ToList();

        Assert.True(allMigrations.Count > 0);

        // This is the key check:
        Assert.Contains(allMigrations, m => m.Contains("RemoveUserWeightKg"));
        Assert.Contains(appliedMigrations, m => m.Contains("RemoveUserWeightKg"));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Users');";

        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();

        while (reader.Read())
            columns.Add(reader.GetString(1)); // column name

        Assert.DoesNotContain("WeightKg", columns);
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

    /// <summary>
    /// CRUD test for User deletion.
    /// </summary>
    [Fact]
    public void DeleteUserRemovesRow()
    {
        using var db = new AppDbContext(_options);

        var (userMeta, user, userKey) = CreateAndSaveUser(db, "delete");

        // Delete
        db.Remove(user);
        db.SaveChanges();

        db.ChangeTracker.Clear();

        var fetched = db.Find(userMeta.UserClrType, userKey);
        Assert.Null(fetched);
    }

    // ----------------------------
    // Entry CRUD tests (Sleep/Diet/Workout)
    // ----------------------------

    [Fact]
    public void SleepEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Sleep");

    [Fact]
    public void DietEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Diet");

    [Fact]
    public void WorkoutEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Workout");

    
    /// <summary>
    /// This test is to check the login functionality by creating a user with a known password
    /// then retrieving that user and verifying the password.
    /// </summary>
    [Fact]
    public void UserLoginTestValid()
    {
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
        using var db = new AppDbContext(_options);

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
    
    /// <summary>
    /// This test verifies that the DietStore's AddWithItemsAsync method correctly saves a DietEntry
    /// along with its associated MealItemEntries to the database.
    /// </summary>
    [Fact]
    public async Task AddWithItemsAsync_PersistsMealAndItems()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser("diet1@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var store = new DietStore(_factory);

        var meal = new DietEntry
        {
            UserId = user.Id,
            EatenAt = DateTime.UtcNow,
            MealType = "Lunch",
            Notes = "Post-workout meal"
        };

        var items = new[]
        {
            new MealItemEntry
            {
                Name = "Chicken",
                Quantity = 200,
                Unit = "g",
                Calories = 330,
                ProteinGrams = 62,
                CarbsGrams = 0,
                FatGrams = 7
            },
            new MealItemEntry
            {
                Name = "Rice",
                Quantity = 150,
                Unit = "g",
                Calories = 195,
                ProteinGrams = 4,
                CarbsGrams = 42,
                FatGrams = 0.5f
            }
        };

        var mealId = await store.AddWithItemsAsync(meal, items);
        var saved = await store.GetWithItemsAsync(mealId);

        Assert.NotNull(saved);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal("Lunch", saved.MealType);
        Assert.Equal(2, saved.Items.Count);
        Assert.Contains(saved.Items, i => i.Name == "Chicken");
        Assert.Contains(saved.Items, i => i.Name == "Rice");
    }

    /// <summary>
    /// This test is to ensure that when listing diet entries for a specific user,
    /// only the meals associated with that user are returned
    /// </summary>
    [Fact]
    public async Task ListForUserAsync_ReturnsOnlyThatUsersMeals()
    {
        await using var db = new AppDbContext(_options);

        var user1 = MakeUser("diet2a@example.com");
        var user2 = MakeUser("diet2b@example.com");

        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        db.DietEntries.AddRange(
            new DietEntry
            {
                UserId = user1.Id,
                EatenAt = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                MealType = "Lunch",
                Notes = "User1 meal"
            },
            new DietEntry
            {
                UserId = user2.Id,
                EatenAt = new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc),
                MealType = "Lunch",
                Notes = "User2 meal"
            });

        await db.SaveChangesAsync();

        var store = new DietStore(_factory);

        var results = await store.ListForUserAsync(
            user1.Id,
            new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 16, 23, 59, 59, DateTimeKind.Utc),
            includeItems: false);

        Assert.Single(results);
        Assert.Equal(user1.Id, results[0].UserId);
        Assert.Equal("User1 meal", results[0].Notes);
    }

    /// <summary>
    /// This test checks to see if the DietStore's UpdateWithItemsAsync method correctly updates the fields of a DietEntry
    /// </summary>
    [Fact]
    public async Task UpdateWithItemsAsync_ReplacesItemsAndUpdatesMealFields()
    {
        var store = new DietStore(_factory);

        await using var db = new AppDbContext(_options);
        var user = MakeUser("diet3@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var originalMeal = new DietEntry
        {
            UserId = user.Id,
            EatenAt = new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc),
            MealType = "Breakfast",
            Notes = "Original"
        };

        var originalItems = new[]
        {
            new MealItemEntry
            {
                Name = "Oats",
                Quantity = 1,
                Unit = "cup",
                Calories = 300,
                ProteinGrams = 10,
                CarbsGrams = 54,
                FatGrams = 5
            }
        };

        var mealId = await store.AddWithItemsAsync(originalMeal, originalItems);

        var updatedMeal = new DietEntry
        {
            Id = mealId,
            UserId = user.Id,
            EatenAt = new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc),
            MealType = "Brunch",
            Notes = "Updated"
        };

        var newItems = new[]
        {
            new MealItemEntry
            {
                Name = "Eggs",
                Quantity = 3,
                Unit = "count",
                Calories = 210,
                ProteinGrams = 18,
                CarbsGrams = 1,
                FatGrams = 15
            }
        };

        await store.UpdateWithItemsAsync(updatedMeal, newItems);

        var saved = await store.GetWithItemsAsync(mealId);

        Assert.NotNull(saved);
        Assert.Equal("Brunch", saved.MealType);
        Assert.Equal("Updated", saved.Notes);
        Assert.Single(saved.Items);
        Assert.Equal("Eggs", saved.Items[0].Name);
    }

    [Fact]
    public async Task SleepService_SaveAsync_Create_AddsNewEntryForCurrentUser()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var service = CreateSleepService(store, userId);

        var input = MakeSleepInput(
            start: new DateTime(2026, 4, 1, 22, 30, 0, DateTimeKind.Utc),
            end:   new DateTime(2026, 4, 2, 6, 45, 0, DateTimeKind.Utc),
            quality: 4.5f,
            notes: "  Slept well  ");

        var result = await service.SaveAsync(input);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        Assert.Single(store.Entries);
        Assert.Equal(1, store.AddCalls);
        Assert.Equal(0, store.UpdateCalls);

        var saved = store.Entries.Single();
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(DateOnly.FromDateTime(input.StartTime), saved.SleepDate);
        Assert.Equal(input.StartTime, saved.StartTime);
        Assert.Equal(input.EndTime, saved.EndTime);
        Assert.Equal("Slept well", saved.Notes);

        // 4.5 * 32 = 144
        Assert.Equal((byte)144, saved.SleepQuality);
    }

    [Fact]
    public async Task SleepService_SaveAsync_Update_SameDateUpdatesExistingEntryInsteadOfAddingAnother()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 7,
            UserId = userId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 96, // 3.0
            Notes = "Original"
        });

        var service = CreateSleepService(store, userId);

        var input = MakeSleepInput(
            start: new DateTime(2026, 4, 1, 23, 0, 0, DateTimeKind.Utc),
            end:   new DateTime(2026, 4, 2, 7, 30, 0, DateTimeKind.Utc),
            quality: 4.0f,
            notes: "  Updated note  ");

        var result = await service.SaveAsync(input);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        Assert.Single(store.Entries);
        Assert.Equal(0, store.AddCalls);
        Assert.Equal(1, store.UpdateCalls);

        var saved = store.Entries.Single();
        Assert.Equal(7, saved.Id);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(new DateOnly(2026, 4, 1), saved.SleepDate);
        Assert.Equal(input.StartTime, saved.StartTime);
        Assert.Equal(input.EndTime, saved.EndTime);
        Assert.Equal("Updated note", saved.Notes);

        // 4.0 * 32 = 128
        Assert.Equal((byte)128, saved.SleepQuality);
    }

    [Fact]
    public async Task SleepService_ListHistoryAsync_Retrieve_ReturnsOnlyCurrentUsersEntriesInDescendingDateOrder()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        store.Seed(
            new SleepEntry
            {
                Id = 1,
                UserId = userId,
                SleepDate = new DateOnly(2026, 4, 1),
                StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
                SleepQuality = 128, // 4.0
                Notes = "Older"
            },
            new SleepEntry
            {
                Id = 2,
                UserId = userId,
                SleepDate = new DateOnly(2026, 4, 3),
                StartTime = new DateTime(2026, 4, 3, 23, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 4, 7, 30, 0, DateTimeKind.Utc),
                SleepQuality = 160, // 5.0
                Notes = "Newest"
            },
            new SleepEntry
            {
                Id = 3,
                UserId = otherUserId,
                SleepDate = new DateOnly(2026, 4, 2),
                StartTime = new DateTime(2026, 4, 2, 22, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 3, 6, 0, 0, DateTimeKind.Utc),
                SleepQuality = 64, // 2.0
                Notes = "Other user"
            });

        var service = CreateSleepService(store, userId);

        var history = await service.ListHistoryAsync(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 5));

        Assert.Equal(2, history.Count);

        Assert.Equal(new DateOnly(2026, 4, 3), history[0].SleepDate);
        Assert.Equal(new DateOnly(2026, 4, 1), history[1].SleepDate);

        Assert.All(history, item => Assert.DoesNotContain("Other user", item.Notes));

        Assert.Equal("Newest", history[0].Notes);
        Assert.Equal("Older", history[1].Notes);
        Assert.True(Math.Abs(history[0].SleepQuality - 5.0f) < 0.001f);
        Assert.True(Math.Abs(history[1].SleepQuality - 4.0f) < 0.001f);
    }

    [Fact]
    public async Task SleepService_DeleteAsync_Delete_RemovesOwnedEntry()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 10,
            UserId = userId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 128,
            Notes = "Delete me"
        });

        var service = CreateSleepService(store, userId);

        var result = await service.DeleteAsync(10);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(1, store.DeleteCalls);
        Assert.Empty(store.Entries);
    }

    [Fact]
    public async Task SleepService_DeleteAsync_UserIsolation_DoesNotDeleteOtherUsersEntry()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 25,
            UserId = otherUserId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 128,
            Notes = "Other user's entry"
        });

        var service = CreateSleepService(store, userId);

        var result = await service.DeleteAsync(25);

        Assert.False(result.Success);
        Assert.Equal("Sleep entry not found.", result.Error);
        Assert.Equal(0, store.DeleteCalls);
        Assert.Single(store.Entries);
        Assert.Equal(25, store.Entries.Single().Id);
    }
    
    [Fact]
    public async Task DietService_CreateMealAsync_PersistsMealAndComputedItems()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"dietsvc_create_{Guid.NewGuid():N}@example.com");
        user.Age = 20;
        user.Sex = Sex.Male;
        user.HeightM = 1.80f;

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateDietService(user.Id);

        var result = await service.CreateMealAsync(new DietMealInput
        {
            EatenAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc),
            MealType = " Lunch ",
            Notes = "  Post workout  ",
            Items =
            [
                new DietMealItemInput
                {
                    Name = " Chicken ",
                    Quantity = 2,
                    Unit = "serving",
                    CaloriesPerUnit = 150,
                    ProteinGrams = 30,
                    CarbsGrams = 0,
                    FatGrams = 5
                },

                new DietMealItemInput
                {
                    Name = " Rice ",
                    Quantity = 1.5f,
                    Unit = "cup",
                    CaloriesPerUnit = 200,
                    ProteinGrams = 4,
                    CarbsGrams = 45,
                    FatGrams = 1
                }
            ]
        });

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.MealId);

        var saved = await db.DietEntries
            .Include(m => m.Items)
            .SingleAsync(m => m.Id == result.MealId!.Value);

        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal("Lunch", saved.MealType);
        Assert.Equal("Post workout", saved.Notes);
        Assert.Equal(2, saved.Items.Count);

        Assert.Contains(saved.Items, i =>
            i is { Name: "Chicken", Quantity: 2, Unit: "serving", Calories: 300 });

        Assert.Contains(saved.Items, i =>
            i is { Name: "Rice", Quantity: 1.5f, Unit: "cup", Calories: 300 });
    }

    [Fact]
    public async Task DietService_ListMealsAsync_ReturnsOnlyCurrentUsersMealsWithAggregates()
    {
        await using var db = new AppDbContext(_options);

        var user1 = MakeUser($"dietsvc_list_a_{Guid.NewGuid():N}@example.com");
        var user2 = MakeUser($"dietsvc_list_b_{Guid.NewGuid():N}@example.com");

        db.Users.AddRange(user1, user2);

        db.DietEntries.AddRange(
            new DietEntry
            {
                UserId = user1.Id,
                EatenAt = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
                MealType = "Breakfast",
                Notes = "Mine",
                Items =
                [
                    new MealItemEntry
                    {
                        Name = "Oats",
                        Quantity = 1,
                        Unit = "cup",
                        Calories = 300,
                        ProteinGrams = 10,
                        CarbsGrams = 54,
                        FatGrams = 5
                    },

                    new MealItemEntry
                    {
                        Name = "Milk",
                        Quantity = 1,
                        Unit = "cup",
                        Calories = 100,
                        ProteinGrams = 8,
                        CarbsGrams = 12,
                        FatGrams = 3
                    }
                ]
            },
            new DietEntry
            {
                UserId = user2.Id,
                EatenAt = new DateTime(2026, 4, 3, 9, 0, 0, DateTimeKind.Utc),
                MealType = "Breakfast",
                Notes = "Not mine",
                Items =
                [
                    new MealItemEntry
                    {
                        Name = "Toast",
                        Quantity = 2,
                        Unit = "slice",
                        Calories = 180,
                        ProteinGrams = 6,
                        CarbsGrams = 30,
                        FatGrams = 2
                    }
                ]
            });

        await db.SaveChangesAsync();

        var service = CreateDietService(user1.Id);

        var results = await service.ListMealsAsync(
            new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 3, 23, 59, 59, DateTimeKind.Utc));

        Assert.Single(results);

        var meal = results[0];
        Assert.Equal("Breakfast", meal.MealType);
        Assert.Equal("Mine", meal.Notes);
        Assert.Equal(2, meal.ItemCount);
        Assert.Equal(400, meal.TotalCalories);
        Assert.Equal(18, meal.TotalProteinGrams);
        Assert.Equal(66, meal.TotalCarbsGrams);
        Assert.Equal(8, meal.TotalFatGrams);
    }

    [Fact]
    public async Task DietService_GetSummaryAsync_ReturnsTotalsAndDerivedTarget()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"dietsvc_summary_{Guid.NewGuid():N}@example.com");
        user.Age = 20;
        user.Sex = Sex.Male;
        user.HeightM = 1.80f;

        db.Users.Add(user);

        db.WeightEntries.AddRange(
            new WeightEntry
            {
                UserId = user.Id,
                Date = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                WeightKg = 80
            },
            new WeightEntry
            {
                UserId = user.Id,
                Date = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
                WeightKg = 82
            });

        db.DietEntries.AddRange(
            new DietEntry
            {
                UserId = user.Id,
                EatenAt = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
                MealType = "Breakfast",
                Notes = "Breakfast",
                Items =
                [
                    new MealItemEntry
                    {
                        Name = "Oats", Quantity = 1, Unit = "cup", Calories = 300, ProteinGrams = 10, CarbsGrams = 54,
                        FatGrams = 5
                    },
                    new MealItemEntry
                    {
                        Name = "Milk", Quantity = 1, Unit = "cup", Calories = 100, ProteinGrams = 8, CarbsGrams = 12,
                        FatGrams = 3
                    }
                ]
            },
            new DietEntry
            {
                UserId = user.Id,
                EatenAt = new DateTime(2026, 4, 3, 18, 0, 0, DateTimeKind.Utc),
                MealType = "Dinner",
                Notes = "Dinner",
                Items =
                [
                    new MealItemEntry
                    {
                        Name = "Chicken", Quantity = 1, Unit = "plate", Calories = 300, ProteinGrams = 50,
                        CarbsGrams = 0, FatGrams = 8
                    },
                    new MealItemEntry
                    {
                        Name = "Rice", Quantity = 1, Unit = "plate", Calories = 250, ProteinGrams = 5, CarbsGrams = 55,
                        FatGrams = 1
                    }
                ]
            });

        await db.SaveChangesAsync();

        var service = CreateDietService(user.Id);

        var summary = await service.GetSummaryAsync(
            new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 3, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(2, summary.MealCount);
        Assert.Equal(4, summary.ItemCount);
        Assert.Equal(950, summary.TotalCalories);
        Assert.Equal(73, summary.TotalProteinGrams);
        Assert.Equal(121, summary.TotalCarbsGrams);
        Assert.Equal(17, summary.TotalFatGrams);
        Assert.Equal(475, summary.AverageCaloriesPerMeal);

        // 10*82 + 6.25*180 - 5*20 + 5 = 1850; sedentary factor 1.2 => 2220
        Assert.Equal(2220, summary.DailyCalorieTarget);
        Assert.Equal(1270, summary.CaloriesRemaining);
        Assert.Contains("Mifflin-St Jeor", summary.TargetMethodDescription);
    }

    [Fact]
    public async Task DietService_GetSummaryAsync_RefreshesAgainstLatestDatabaseState()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"dietsvc_refresh_{Guid.NewGuid():N}@example.com");
        user.Age = 20;
        user.Sex = Sex.Male;
        user.HeightM = 1.80f;

        db.Users.Add(user);
        db.DietEntries.Add(new DietEntry
        {
            UserId = user.Id,
            EatenAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc),
            MealType = "Lunch",
            Notes = "Refresh test",
            Items =
            [
                new MealItemEntry
                {
                    Name = "Meal",
                    Quantity = 1,
                    Unit = "serving",
                    Calories = 500,
                    ProteinGrams = 30,
                    CarbsGrams = 40,
                    FatGrams = 10
                }
            ]
        });

        await db.SaveChangesAsync();

        var service = CreateDietService(user.Id);

        var before = await service.GetSummaryAsync(
            new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 3, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(1, before.MealCount);
        Assert.Null(before.DailyCalorieTarget);

        db.WeightEntries.Add(new WeightEntry
        {
            UserId = user.Id,
            Date = new DateTime(2026, 4, 3, 20, 0, 0, DateTimeKind.Utc),
            WeightKg = 90
        });

        await db.SaveChangesAsync();

        var after = await service.GetSummaryAsync(
            new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 3, 23, 59, 59, DateTimeKind.Utc));

        Assert.NotNull(after.DailyCalorieTarget);
        Assert.NotEqual(before.DailyCalorieTarget, after.DailyCalorieTarget);
    }
    
    [Fact]
    public async Task AccountService_Login_ValidCredentials_SetsCurrentUser()
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
    public async Task AccountService_Register_DuplicateEmail_ReturnsFriendlyError()
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
    
    [Fact]
    public async Task WorkoutService_AddWorkoutAsync_PersistsWorkoutAndMappedExercises()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"workout_add_{Guid.NewGuid():N}@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user.Id);

        var exercise = MakeWorkoutExerciseInput(
            name: "  Treadmill Run  ",
            sets: 0,
            reps: 0,
            weightKg: 0,
            distanceKm: 5,
            durationMinutes: 30);

        var input = MakeWorkoutInput(
            startedAt: new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc),
            durationMinutes: 45,
            selfReportedIntensity: 4,
            averageHeartRate: 150,
            workoutType: WorkoutType.Cardio,
            notes: "  Good session  ",
            exercise);

        var result = await service.AddWorkoutAsync(input);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.WorkoutId);

        var saved = await db.WorkoutEntries
            .Include(w => w.Exercises)
            .SingleAsync(w => w.Id == result.WorkoutId!.Value);

        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc), saved.StartedAt);
        Assert.Equal(45, saved.DurationMinutes);
        Assert.Equal((byte)4, saved.SelfReportedIntensity);
        Assert.Equal(150, saved.AverageHeartRate);
        Assert.Equal(WorkoutType.Cardio, saved.WorkoutType);
        Assert.Equal("Good session", saved.Notes);

        Assert.Single(saved.Exercises);
        Assert.Equal("Treadmill Run", saved.Exercises[0].Name);
        Assert.Equal(TimeSpan.FromMinutes(30), saved.Exercises[0].Time);
        Assert.Equal(5, saved.Exercises[0].DistanceKm);
    }

    [Fact]
    public async Task WorkoutService_ListPastWorkoutsAsync_ReturnsOnlyCurrentUsersWorkoutsInDescendingOrder()
    {
        await using var db = new AppDbContext(_options);

        var user1 = MakeUser($"workout_list_a_{Guid.NewGuid():N}@example.com");
        var user2 = MakeUser($"workout_list_b_{Guid.NewGuid():N}@example.com");

        db.Users.AddRange(user1, user2);

        db.WeightEntries.Add(new WeightEntry
        {
            UserId = user1.Id,
            Date = new DateTime(2026, 4, 4, 7, 0, 0, DateTimeKind.Utc),
            WeightKg = 80
        });

        var olderExercise = MakeExerciseEntry(
            name: "Bike",
            sets: 0,
            reps: 0,
            weightKg: 0,
            distanceKm: 10,
            durationMinutes: 20);

        var newerExercise = MakeExerciseEntry(
            name: "Run",
            sets: 0,
            reps: 0,
            weightKg: 0,
            distanceKm: 4,
            durationMinutes: 30);

        db.WorkoutEntries.AddRange(
            new WorkoutEntry
            {
                UserId = user1.Id,
                StartedAt = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 25,
                SelfReportedIntensity = 3,
                AverageHeartRate = 135,
                WorkoutType = WorkoutType.Cardio,
                Notes = "Older workout",
                Exercises = [olderExercise]
            },
            new WorkoutEntry
            {
                UserId = user1.Id,
                StartedAt = new DateTime(2026, 4, 4, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 35,
                SelfReportedIntensity = 4,
                AverageHeartRate = 150,
                WorkoutType = WorkoutType.Cardio,
                Notes = "Newest workout",
                Exercises = [newerExercise]
            },
            new WorkoutEntry
            {
                UserId = user2.Id,
                StartedAt = new DateTime(2026, 4, 4, 11, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 40,
                SelfReportedIntensity = 5,
                AverageHeartRate = 160,
                WorkoutType = WorkoutType.Cardio,
                Notes = "Other user workout",
                Exercises = [MakeExerciseEntry("Other", 0, 0, 0, 2, 15)]
            });

        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user1.Id);

        var results = await service.ListPastWorkoutsAsync(
            new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 4, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(2, results.Count);
        Assert.Equal("Newest workout", results[0].Notes);
        Assert.Equal("Older workout", results[1].Notes);
        Assert.All(results, r => Assert.NotEqual("Other user workout", r.Notes));

        Assert.Equal(1, results[0].ExerciseCount);
        Assert.Equal(1, results[1].ExerciseCount);
    }

    [Fact]
    public async Task WorkoutService_GetWorkoutAsync_ComputesExerciseAndTotalCalories()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"workout_detail_{Guid.NewGuid():N}@example.com");
        db.Users.Add(user);

        db.WeightEntries.Add(new WeightEntry
        {
            UserId = user.Id,
            Date = new DateTime(2026, 4, 4, 7, 0, 0, DateTimeKind.Utc),
            WeightKg = 80
        });

        var exercise1 = MakeExerciseEntry("Run", 0, 0, 0, 5, 30);
        var exercise2 = MakeExerciseEntry("Bike", 0, 0, 0, 8, 20);

        var workout = new WorkoutEntry
        {
            UserId = user.Id,
            StartedAt = new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 50,
            SelfReportedIntensity = 4,
            AverageHeartRate = 148,
            WorkoutType = WorkoutType.Cardio,
            Notes = "Detail workout",
            Exercises = [exercise1, exercise2]
        };

        db.WorkoutEntries.Add(workout);
        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user.Id);

        var result = await service.GetWorkoutAsync(workout.Id);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.Workout);

        var detail = result.Workout!;
        Assert.Equal(80, detail.UserWeightKg);
        Assert.Equal(2, detail.Exercises.Count);

        var expected1 = ComputeExpectedCalories(80, 30, GetActivityTypeValue(exercise1));
        var expected2 = ComputeExpectedCalories(80, 20, GetActivityTypeValue(exercise2));

        Assert.Equal(expected1, detail.Exercises[0].EstimatedCaloriesBurned);
        Assert.Equal(expected2, detail.Exercises[1].EstimatedCaloriesBurned);

        Assert.NotNull(expected1);
        Assert.NotNull(expected2);

        var expectedTotal = Math.Round((expected1 ?? 0) + (expected2 ?? 0), 2);

        Assert.Equal(expectedTotal, detail.EstimatedCaloriesBurned);
    }

    [Fact]
    public async Task WorkoutService_UpdateWorkoutAsync_UpdatesMetadata_WhenExercisesUnchanged()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"workout_update_{Guid.NewGuid():N}@example.com");
        db.Users.Add(user);

        var originalExercise = MakeExerciseEntry("Bench Press", 3, 8, 80, 0, 0);

        var workout = new WorkoutEntry
        {
            UserId = user.Id,
            StartedAt = new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 60,
            SelfReportedIntensity = 3,
            AverageHeartRate = 120,
            WorkoutType = WorkoutType.StrengthTraining,
            Notes = "Original",
            Exercises = [originalExercise]
        };

        db.WorkoutEntries.Add(workout);
        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user.Id);

        var sameExerciseInput = MakeWorkoutExerciseInput(
            name: "Bench Press",
            sets: 3,
            reps: 8,
            weightKg: 80,
            distanceKm: 0,
            durationMinutes: 0);

        CopyActivityType(originalExercise, sameExerciseInput);

        var input = MakeWorkoutInput(
            startedAt: new DateTime(2026, 4, 4, 10, 30, 0, DateTimeKind.Utc),
            durationMinutes: 75,
            selfReportedIntensity: 5,
            averageHeartRate: 132,
            workoutType: WorkoutType.StrengthTraining,
            notes: "  Updated note  ",
            sameExerciseInput);

        var result = await service.UpdateWorkoutAsync(workout.Id, input);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        db.ChangeTracker.Clear();

        var saved = await db.WorkoutEntries
            .Include(w => w.Exercises)
            .SingleAsync(w => w.Id == workout.Id);

        Assert.Equal(new DateTime(2026, 4, 4, 10, 30, 0, DateTimeKind.Utc), saved.StartedAt);
        Assert.Equal(75, saved.DurationMinutes);
        Assert.Equal((byte)5, saved.SelfReportedIntensity);
        Assert.Equal(132, saved.AverageHeartRate);
        Assert.Equal("Updated note", saved.Notes);
        Assert.Single(saved.Exercises);
        Assert.Equal("Bench Press", saved.Exercises[0].Name);
    }

    [Fact]
    public async Task WorkoutService_UpdateWorkoutAsync_RejectsExerciseChanges()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"workout_update_reject_{Guid.NewGuid():N}@example.com");
        db.Users.Add(user);

        var originalExercise = MakeExerciseEntry("Row", 3, 10, 50, 0, 0);

        var workout = new WorkoutEntry
        {
            UserId = user.Id,
            StartedAt = new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 45,
            SelfReportedIntensity = 3,
            AverageHeartRate = 125,
            WorkoutType = WorkoutType.StrengthTraining,
            Notes = "Original",
            Exercises = [originalExercise]
        };

        db.WorkoutEntries.Add(workout);
        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user.Id);

        var changedExerciseInput = MakeWorkoutExerciseInput(
            name: "Row",
            sets: 4, // changed from 3
            reps: 10,
            weightKg: 50,
            distanceKm: 0,
            durationMinutes: 0);

        CopyActivityType(originalExercise, changedExerciseInput);

        var input = MakeWorkoutInput(
            startedAt: workout.StartedAt,
            durationMinutes: workout.DurationMinutes,
            selfReportedIntensity: workout.SelfReportedIntensity,
            averageHeartRate: workout.AverageHeartRate,
            workoutType: workout.WorkoutType,
            notes: workout.Notes,
            changedExerciseInput);

        var result = await service.UpdateWorkoutAsync(workout.Id, input);

        Assert.False(result.Success);
        Assert.Equal("Updating exercises is not supported yet. Keep the existing exercises unchanged or recreate the workout.", result.Error);
    }

    [Fact]
    public async Task WorkoutService_DeleteWorkoutAsync_RemovesOwnedWorkout()
    {
        await using var db = new AppDbContext(_options);

        var user = MakeUser($"workout_delete_{Guid.NewGuid():N}@example.com");
        db.Users.Add(user);

        var workout = new WorkoutEntry
        {
            UserId = user.Id,
            StartedAt = new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 30,
            SelfReportedIntensity = 2,
            AverageHeartRate = 118,
            WorkoutType = WorkoutType.Cardio,
            Notes = "Delete me",
            Exercises = [MakeExerciseEntry("Walk", 0, 0, 0, 2, 30)]
        };

        db.WorkoutEntries.Add(workout);
        await db.SaveChangesAsync();

        var service = CreateWorkoutService(user.Id);

        var result = await service.DeleteWorkoutAsync(workout.Id);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        db.ChangeTracker.Clear();

        var deleted = await db.WorkoutEntries.SingleOrDefaultAsync(w => w.Id == workout.Id);
        Assert.Null(deleted);
    }
    
    [Fact]
    public void Parse_StandardNutritionLabel_ExtractsExpectedFields()
    {
        var parser = new Parser();

        const string text = """
                            Nutrition Facts
                            8 servings per container
                            Serving size 2/3 cup (55g)

                            Calories 230
                            Total Fat 8g
                            Total Carbohydrate 37g
                            Protein 3g
                            """;

        var result = parser.Parse(text, "Granola");

        Assert.Equal("Granola", result.Name);
        Assert.Equal(230, result.Calories);
        Assert.Equal(8f, result.FatGrams);
        Assert.Equal(37f, result.CarbsGrams);
        Assert.Equal(3f, result.ProteinGrams);
        Assert.Equal(8f, result.ServingsPerContainer);
        Assert.Equal(2f / 3f, result.ServingSizeAmount!.Value, 3);
        Assert.Equal("cup", result.ServingSizeUnit);
        Assert.Equal(55f, result.MetricServingAmount);
        Assert.Equal("g", result.MetricServingUnit);
    }

    [Fact]
    public void Parse_OcrStyleTextWithDifferentSpacing_StillExtractsFields()
    {
        var parser = new Parser();

        const string text = """
                            NUTRITION FACTS
                            Servings per container: about 2.5
                            Serving size: 1 container (150 g)

                            Calorles 190
                            Total Fat 4g
                            Carbs 28g
                            Proteln 9g
                            """;

        var result = parser.Parse(text, "Yogurt");

        Assert.Equal(190, result.Calories);
        Assert.Equal(4f, result.FatGrams);
        Assert.Equal(28f, result.CarbsGrams);
        Assert.Equal(9f, result.ProteinGrams);
        Assert.Equal(2.5f, result.ServingsPerContainer);
        Assert.Equal(1f, result.ServingSizeAmount);
        Assert.Equal("container", result.ServingSizeUnit);
        Assert.Equal(150f, result.MetricServingAmount);
        Assert.Equal("g", result.MetricServingUnit);
    }

    [Fact]
    public void Parse_MissingFields_AddsWarningsInsteadOfThrowing()
    {
        var parser = new Parser();

        const string text = """
                            Nutrition Facts
                            Serving size 1 bar (40g)
                            Calories 180
                            """;

        var result = parser.Parse(text, "Protein Bar");

        Assert.Equal(180, result.Calories);
        Assert.Null(result.ProteinGrams);
        Assert.Null(result.CarbsGrams);
        Assert.Null(result.FatGrams);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("Protein", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("Carbohydrates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("Fat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToDietMealItemInput_DefaultsToOneServing()
    {
        var parsed = new ParsedResult
        {
            Name = "Crackers",
            Calories = 160,
            ProteinGrams = 2,
            CarbsGrams = 22,
            FatGrams = 7
        };

        var item = parsed.ToDietMealItemInput();

        Assert.Equal("Crackers", item.Name);
        Assert.Equal(1f, item.Quantity);
        Assert.Equal("serving", item.Unit);
        Assert.Equal(160, item.CaloriesPerUnit);
        Assert.Equal(2f, item.ProteinGrams);
        Assert.Equal(22f, item.CarbsGrams);
        Assert.Equal(7f, item.FatGrams);
    }

    // ----------------------------
    // Metadata + reflection helpers
    // ----------------------------

    private static UserEntry MakeUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Test User",
        PasswordHash = "testhash",
        CreationDate = DateTime.UtcNow,
        LastOnline = DateTime.UtcNow,
        Age = 20,
        Sex = Sex.Undefined,
        HeightM = 1.75f
    };
    
    private void EntryCrudWorks(string kind)
    {
        using var db = new AppDbContext(_options);

        var (userMeta, user, userKey) = CreateAndSaveUser(db, kind.ToLowerInvariant());

        var entryMeta = GetEntryMeta(db, kind, userMeta);

        var entry = CreateEntryInstance(db, entryMeta, user, userKey, kind);
        db.Add(entry);
        db.SaveChanges();

        var entryKey = GetPropValue(entry, entryMeta.KeyPropName)
                       ?? throw new InvalidOperationException($"{kind} entry key was null after SaveChanges().");

        var mutableOriginal = GetPropValue(entry, entryMeta.MutablePropName);

        // Read
        db.ChangeTracker.Clear();
        var fetched = db.Find(entryMeta.EntryClrType, entryKey)
                     ?? throw new InvalidOperationException($"Could not find {kind} entry by primary key after insertion.");

        Assert.Equal(entryKey, GetPropValue(fetched, entryMeta.KeyPropName));
        Assert.Equal(mutableOriginal, GetPropValue(fetched, entryMeta.MutablePropName));

        // Update
        var tracked = db.Find(entryMeta.EntryClrType, entryKey)
                     ?? throw new InvalidOperationException($"Could not re-load {kind} entry by key for update.");

        var mutableProp = entryMeta.EntryClrType.GetProperty(entryMeta.MutablePropName, BindingFlags.Instance | BindingFlags.Public)
                         ?? throw new InvalidOperationException($"Mutable property '{entryMeta.MutablePropName}' not found on {kind} entity type.");

        var updatedValue = MakeUpdatedValue(mutableProp.PropertyType, GetPropValue(tracked, entryMeta.MutablePropName));
        SetConvertedPropValue(tracked, entryMeta.MutablePropName, updatedValue);
        db.SaveChanges();

        db.ChangeTracker.Clear();
        var fetchedAfterUpdate = db.Find(entryMeta.EntryClrType, entryKey)
                               ?? throw new InvalidOperationException($"Could not re-load {kind} entry after update.");

        Assert.Equal(updatedValue, GetPropValue(fetchedAfterUpdate, entryMeta.MutablePropName));

        // Delete
        var toRemove = db.Find(entryMeta.EntryClrType, entryKey)
                      ?? throw new InvalidOperationException($"Could not re-load {kind} entry by key for delete.");

        db.Remove(toRemove);
        db.SaveChanges();

        db.ChangeTracker.Clear();
        var deleted = db.Find(entryMeta.EntryClrType, entryKey);
        Assert.Null(deleted);
    }
    
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

        var emailPropName = PickFirstExistingProperty(userEntity.ClrType, "Email", "EmailAddress")
                            ?? throw new InvalidOperationException("User entity does not contain an Email or EmailAddress property.");

        var displayNamePropName = PickFirstExistingProperty(userEntity.ClrType, "DisplayName", "Name", "Username")
                                  ?? throw new InvalidOperationException("User entity does not contain DisplayName/Name/Username property. Add one or update the test.");

        var passwordPropName = PickFirstExistingProperty(
            userEntity.ClrType,
            "PasswordHash",
            "HashedPassword",
            "Password");

        return new UserMeta(userEntity.ClrType, keyPropName, emailPropName, displayNamePropName, passwordPropName);
    }
    
    private static void SetUserPassword(object user, UserMeta meta, string rawPassword)
    {
        if (string.IsNullOrWhiteSpace(meta.PasswordPropName)) return;

        var prop = user.GetType().GetProperty(meta.PasswordPropName!, BindingFlags.Instance | BindingFlags.Public)
                   ?? throw new InvalidOperationException(
                       $"Password property '{meta.PasswordPropName}' not found on type '{user.GetType().Name}'.");

        string storedValue;

        // If the property name suggests a hash, hash it.
        if (prop.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase))
        {
            var hasher = new PasswordHasher<object>();
            storedValue = hasher.HashPassword(user, rawPassword);
        }
        else
        {
            storedValue = rawPassword;
        }

        prop.SetValue(user, storedValue);
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

    private static EntryMeta GetEntryMeta(AppDbContext db, string kind, UserMeta userMeta)
    {
        var entityTypes = db.Model.GetEntityTypes().ToList();
        var userEntityType = db.Model.FindEntityType(userMeta.UserClrType)
                            ?? throw new InvalidOperationException("Could not locate User entity type in EF model.");

        var candidates = entityTypes
            .Where(et => et.ClrType.Name.Contains(kind, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Could not locate an entity type whose CLR name contains '{kind}'. Ensure you have a mapped '{kind}' entry entity (e.g., {kind}Entry).");
        }

        IEntityType? best = null;
        var bestScore = int.MinValue;

        foreach (var et in candidates)
        {
            if (et.FindPrimaryKey() is null) continue;

            var score = 0;
            var name = et.ClrType.Name;
            if (name.Equals(kind, StringComparison.OrdinalIgnoreCase)) score += 4;
            if (name.EndsWith(kind, StringComparison.OrdinalIgnoreCase)) score += 2;
            if (name.EndsWith($"{kind}Entry", StringComparison.OrdinalIgnoreCase)) score += 3;

            var hasUserFk = et.GetForeignKeys().Any(fk => fk.PrincipalEntityType == userEntityType && fk.Properties.Count == 1);
            if (hasUserFk) score += 5;

            var hasEditableScalar = et.GetProperties().Any(p =>
                p.PropertyInfo is not null && p.PropertyInfo.CanWrite &&
                p.ValueGenerated == ValueGenerated.Never &&
                (p.ClrType == typeof(string) || p.ClrType == typeof(int) || p.ClrType == typeof(long) || p.ClrType == typeof(double) ||
                 p.ClrType == typeof(float) || p.ClrType == typeof(decimal) || p.ClrType == typeof(DateTime) || p.ClrType == typeof(TimeSpan) ||
                 p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(Guid)));

            if (hasEditableScalar) score += 2;

            if (score <= bestScore) continue;
            bestScore = score;
            best = et;
        }

        var entity = best ?? candidates[0];

        var pk = entity.FindPrimaryKey()!;
        if (pk.Properties.Count != 1)
            throw new InvalidOperationException($"{kind} entry primary key must be a single-column key for these tests. Found {pk.Properties.Count} columns.");

        var keyPropName = pk.Properties[0].Name;

        // FK to User (best-effort)
        var userFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == userEntityType && fk.Properties.Count == 1);

        var userFkPropName = userFk?.Properties[0].Name;

        // Pick a mutable scalar property to validate + update
        var mutablePropName = PickMutableProperty(entity, keyPropName, userFkPropName)
                              ?? throw new InvalidOperationException(
                                  $"Could not locate a mutable scalar property on '{entity.ClrType.Name}' to use for an update test. " +
                                  "Add a writable scalar property (e.g., Notes/Duration/Calories) or update PickMutableProperty().");

        return new EntryMeta(entity.ClrType, keyPropName, userFkPropName, mutablePropName);
    }

    private static string? PickMutableProperty(IEntityType entity, string keyPropName, string? userFkPropName)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyPropName };
        if (!string.IsNullOrWhiteSpace(userFkPropName)) excluded.Add(userFkPropName);

        // Prefer common "editable" fields first
        var preferredNames = new[]
        {
            "Notes", "Comment", "Description", "Details",
            "Quality", "Duration", "Hours", "Minutes",
            "Calories", "CaloriesBurned", "Protein", "Carbs", "Fat",
            "Steps", "Distance", "Reps", "Sets", "Weight",
            "Name", "Title"
        };

        foreach (var pref in preferredNames)
        {
            var p = entity.GetProperties().FirstOrDefault(x =>
                !excluded.Contains(x.Name) &&
                x.PropertyInfo is not null && x.PropertyInfo.CanWrite &&
                x.ValueGenerated == ValueGenerated.Never &&
                string.Equals(x.Name, pref, StringComparison.OrdinalIgnoreCase));

            if (p is not null) return p.Name;
        }

        // Fallback: first reasonable scalar
        var fallback = entity.GetProperties().FirstOrDefault(p =>
            !excluded.Contains(p.Name) &&
            p.PropertyInfo is not null && p.PropertyInfo.CanWrite &&
            p.ValueGenerated == ValueGenerated.Never &&
            (p.ClrType == typeof(string) || p.ClrType == typeof(int) || p.ClrType == typeof(long) || p.ClrType == typeof(double) ||
             p.ClrType == typeof(float) || p.ClrType == typeof(decimal) || p.ClrType == typeof(DateTime) || p.ClrType == typeof(TimeSpan) ||
             p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(Guid)));

        return fallback?.Name;
    }

    private static (UserMeta meta, object user, object userKey) CreateAndSaveUser(AppDbContext db, string prefix)
    {
        var userMeta = GetUserMeta(db);
        var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
        var display = $"{prefix} user";

        var user = CreateUserInstance(userMeta, email, display);
        db.Add(user);
        db.SaveChanges();

        var userKey = GetPropValue(user, userMeta.KeyPropName)
                      ?? throw new InvalidOperationException("User key was null after SaveChanges().");

        return (userMeta, user, userKey);
    }

    private static object CreateUserInstance(UserMeta meta, string email, string displayName)
    {
        var user = Activator.CreateInstance(meta.UserClrType)
                   ?? throw new InvalidOperationException($"Could not create instance of {meta.UserClrType.FullName} (missing public parameterless constructor?).");

        var keyProp = meta.UserClrType.GetProperty(meta.KeyPropName, BindingFlags.Instance | BindingFlags.Public);
        if (keyProp is not null && keyProp.PropertyType == typeof(Guid))
        {
            var current = (Guid)(keyProp.GetValue(user) ?? Guid.Empty);
            if (current == Guid.Empty)
                keyProp.SetValue(user, Guid.NewGuid());
        }

        SetPropValue(user, meta.EmailPropName, email);
        SetPropValue(user, meta.DisplayNamePropName, displayName);

        // Set a default password if the entity has a password property.
        SetUserPassword(user, meta, "DefaultTestPassword123!");

        return user;
    }

    private static object CreateEntryInstance(AppDbContext db, EntryMeta meta, object userInstance, object userKey, string kind)
    {
        var entry = Activator.CreateInstance(meta.EntryClrType)
                    ?? throw new InvalidOperationException($"Could not create instance of {meta.EntryClrType.FullName} (missing public parameterless constructor?).");

        var entityType = db.Model.FindEntityType(meta.EntryClrType)
                        ?? throw new InvalidOperationException($"Could not locate EF entity type for CLR type {meta.EntryClrType.FullName}.");

        // Ensure Guid PK is set if necessary (helps Find()).
        var keyProp = meta.EntryClrType.GetProperty(meta.KeyPropName, BindingFlags.Instance | BindingFlags.Public);
        if (keyProp is not null && keyProp.PropertyType == typeof(Guid))
        {
            var current = (Guid)(keyProp.GetValue(entry) ?? Guid.Empty);
            if (current == Guid.Empty)
                keyProp.SetValue(entry, Guid.NewGuid());
        }

        // Set FK to User if we can find it.
        if (!string.IsNullOrWhiteSpace(meta.UserFkPropName))
        {
            SetConvertedPropValue(entry, meta.UserFkPropName!, userKey);
        }
        else
        {
            // Fall back to navigation property if a scalar FK wasn't discovered.
            var nav = meta.EntryClrType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.CanWrite && p.PropertyType.IsInstanceOfType(userInstance));

            nav?.SetValue(entry, userInstance);
        }

        // Set required scalar properties to non-default values.
        foreach (var p in entityType.GetProperties())
        {
            if (p.IsPrimaryKey()) continue;
            if (p.ValueGenerated != ValueGenerated.Never) continue; // store-generated
            if (p.PropertyInfo is null || !p.PropertyInfo.CanWrite) continue;

            // Skip FK we already set.
            if (!string.IsNullOrWhiteSpace(meta.UserFkPropName) &&
                string.Equals(p.Name, meta.UserFkPropName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // If it's required (NOT NULL), ensure it's set to something other than default.
            if (p.IsNullable) continue;
            
            var current = GetPropValue(entry, p.Name);
            if (!IsDefaultValue(current, p.ClrType)) continue;
            
            var v = MakeDummyValue(p.ClrType, kind, p.Name);
            SetConvertedPropValue(entry, p.Name, v);
        }

        // Ensure the mutable property has a predictable initial value for the update test.
        var mutableProp = meta.EntryClrType.GetProperty(meta.MutablePropName, BindingFlags.Instance | BindingFlags.Public)
                         ?? throw new InvalidOperationException($"Mutable property '{meta.MutablePropName}' not found on {meta.EntryClrType.Name}.");

        var initialMutable = MakeInitialMutableValue(mutableProp.PropertyType, kind);
        SetConvertedPropValue(entry, meta.MutablePropName, initialMutable);

        return entry;
    }

    private static bool IsDefaultValue(object? value, Type type)
    {
        if (value is null) return true;

        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (!t.IsValueType)
        {
            // Reference type (string, etc.)
            return value is string s && string.IsNullOrEmpty(s);
        }

        var defaultValue = Activator.CreateInstance(t);
        return Equals(value, defaultValue);
    }

    private static object MakeDummyValue(Type type, string kind, string propName)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(string)) return $"{kind}_{propName}_{Guid.NewGuid():N}";
        if (t == typeof(byte)) return (byte)10;
        if (t == typeof(int)) return 10;
        if (t == typeof(long)) return 10L;
        if (t == typeof(short)) return (short)10;
        if (t == typeof(double)) return 10.5;
        if (t == typeof(float)) return 10.5f;
        if (t == typeof(decimal)) return 10.5m;
        if (t == typeof(bool)) return true;
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t == typeof(TimeSpan)) return TimeSpan.FromMinutes(30);
        if (t == typeof(Guid)) return Guid.NewGuid();

        switch (t.FullName)
        {
            // .NET 6+ types
            case "System.DateOnly":
            {
                var dt = DateTime.UtcNow;
                var dateOnlyFrom = t.GetMethod("FromDateTime", BindingFlags.Public | BindingFlags.Static);
                return dateOnlyFrom?.Invoke(null, [dt])
                       ?? throw new InvalidOperationException("Could not construct DateOnly value.");
            }
            case "System.TimeOnly":
            {
                var dt = DateTime.UtcNow;
                var timeOnlyFrom = t.GetMethod("FromDateTime", BindingFlags.Public | BindingFlags.Static);
                return timeOnlyFrom?.Invoke(null, [dt])
                       ?? throw new InvalidOperationException("Could not construct TimeOnly value.");
            }
        }

        if (t.IsEnum)
        {
            var values = Enum.GetValues(t);
            return values.Length > 0
                ? values.GetValue(0)!
                : Activator.CreateInstance(t)!;
        }

        throw new InvalidOperationException($"No dummy value generator for required property type '{t.FullName}'. Property: {propName}.");
    }

    private static object MakeInitialMutableValue(Type type, string kind)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return $"{kind}_original";
        if (t == typeof(int)) return 10;
        if (t == typeof(long)) return 10L;
        if (t == typeof(short)) return (short)10;
        if (t == typeof(double)) return 10.0;
        if (t == typeof(float)) return 10.0f;
        if (t == typeof(decimal)) return 10m;
        if (t == typeof(bool)) return true;
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t == typeof(TimeSpan)) return TimeSpan.FromMinutes(30);
        return t == typeof(Guid) ? Guid.NewGuid() :
            // DateOnly/TimeOnly/enums fallback
            MakeDummyValue(type, kind, "Mutable");
    }

    private static object MakeUpdatedValue(Type type, object? current)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        // I wish this could be a switch expression, this is so ugly... - CT
        if (t == typeof(string)) return $"updated_{Guid.NewGuid():N}";
        if (t == typeof(int)) return current is int i ? i + 1 : 11;
        if (t == typeof(long)) return current is long l ? l + 1 : 11L;
        if (t == typeof(short)) return (short)(current is short s ? s + 1 : 11);
        if (t == typeof(double)) return current is double d ? d + 1.0 : 11.0;
        if (t == typeof(float)) return current is float f ? f + 1.0f : 11.0f;
        if (t == typeof(decimal)) return current is decimal m ? m + 1m : 11m;
        if (t == typeof(bool)) return current is false;
        if (t == typeof(DateTime)) return current is DateTime dt ? dt.AddMinutes(5) : DateTime.UtcNow.AddMinutes(5);
        if (t == typeof(DateTimeOffset)) return current is DateTimeOffset dto ? dto.AddMinutes(5) : DateTimeOffset.UtcNow.AddMinutes(5);
        if (t == typeof(TimeSpan)) return current is TimeSpan ts ? ts + TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(35);
        if (t == typeof(Guid)) return Guid.NewGuid();

        if (!t.IsEnum) return MakeDummyValue(type, "updated", "Mutable");
        var values = Enum.GetValues(t);
        return values.Length switch
        {
            >= 2 => values.GetValue(1)!,
            1 => values.GetValue(0)!,
            // DateOnly/TimeOnly fallback: just generate a new value.
            _ => MakeDummyValue(type, "updated", "Mutable")
        };
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
        if (prop is null) throw new InvalidOperationException($"Property '{propName}' not found on type '{obj.GetType().Name}'.");

        prop.SetValue(obj, value);
    }

    private static void SetConvertedPropValue(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (prop is null) throw new InvalidOperationException($"Property '{propName}' not found on type '{obj.GetType().Name}'.");

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        var converted = value;
        if (!targetType.IsInstanceOfType(value))
        {
            if (targetType == typeof(Guid) && value is string s)
            {
                converted = Guid.Parse(s);
            }
            else if (targetType == typeof(string) && value is Guid g)
            {
                converted = g.ToString();
            }
            else if (targetType.IsEnum)
            {
                converted = value is string es
                    ? Enum.Parse(targetType, es)
                    : Enum.ToObject(targetType, value);
            }
            else
            {
                converted = Convert.ChangeType(value, targetType);
            }
        }

        prop.SetValue(obj, converted);
    }
    
    private static SleepService CreateSleepService(FakeSleepStore store, Guid? userId)
    {
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    authenticationType: "TestAuth"));
        }

        return new SleepService(store, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static SleepEntryInput MakeSleepInput(DateTime start, DateTime end, float quality, string notes = "")
    {
        return new SleepEntryInput
        {
            StartTime = start,
            EndTime = end,
            SleepQuality = quality,
            Notes = notes
        };
    }
    
    private DietService CreateDietService(Guid? userId)
    {
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    "TestAuth"));
        }

        return new DietService(new DietStore(_factory), new AccountStore(_factory), _factory, new HttpContextAccessor { HttpContext = httpContext });
    }
    
    private AccountService CreateAccountService()
    {
        return new AccountService(new AccountStore(_factory));
    }

    private static DefaultHttpContext CreateEndpointContext(
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

    private static ServiceProvider BuildEndpointServices(FakeAuthenticationService auth)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IAuthenticationService>(auth);

        return services.BuildServiceProvider();
    }

    private static async Task InvokePrivateStaticTaskMethod(Type declaringType, string methodName, params object[] args)
    {
        var method = declaringType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find method '{methodName}'.");

        var task = method.Invoke(null, args) as Task
            ?? throw new InvalidOperationException($"Method '{methodName}' did not return Task.");

        await task;
    }

    private static async Task<IResult> InvokePrivateStaticResultMethod(Type declaringType, string methodName, params object[] args)
    {
        var method = declaringType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find method '{methodName}'.");

        var task = method.Invoke(null, args) as Task<IResult>
            ?? throw new InvalidOperationException($"Method '{methodName}' did not return Task<IResult>.");

        return await task;
    }
    
    private WorkoutService CreateWorkoutService(Guid? userId)
    {
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    "TestAuth"));
        }

        return new WorkoutService(new WorkoutStore(_factory), new WeightStore(_factory), new HttpContextAccessor { HttpContext = httpContext });
    }

    private static WorkoutEntryInput MakeWorkoutInput(
        DateTime startedAt,
        int durationMinutes,
        int selfReportedIntensity,
        int averageHeartRate,
        WorkoutType workoutType,
        string notes,
        params WorkoutExerciseInput[] exercises)
    {
        return new WorkoutEntryInput
        {
            StartedAt = startedAt,
            DurationMinutes = durationMinutes,
            SelfReportedIntensity = selfReportedIntensity,
            AverageHeartRate = averageHeartRate,
            WorkoutType = workoutType,
            Notes = notes,
            Exercises = exercises.ToList()
        };
    }

    private static WorkoutExerciseInput MakeWorkoutExerciseInput(
        string name,
        int sets,
        int reps,
        float weightKg,
        float distanceKm,
        int durationMinutes)
    {
        var input = new WorkoutExerciseInput
        {
            Name = name,
            Sets = sets,
            Reps = reps,
            WeightKg = weightKg,
            DistanceKm = distanceKm,
            DurationMinutes = durationMinutes
        };

        TrySetFirstNonDefaultActivityType(input);
        return input;
    }

    private static ExerciseEntry MakeExerciseEntry(
        string name,
        int sets,
        int reps,
        float weightKg,
        float distanceKm,
        int durationMinutes)
    {
        var entry = new ExerciseEntry
        {
            Name = name,
            Sets = sets,
            Reps = reps,
            WeightKg = weightKg,
            DistanceKm = distanceKm,
            Time = TimeSpan.FromMinutes(durationMinutes)
        };

        TrySetFirstNonDefaultActivityType(entry);
        return entry;
    }

    private static void TrySetFirstNonDefaultActivityType(object target)
    {
        var prop = target.GetType().GetProperty("ActivityType", BindingFlags.Instance | BindingFlags.Public);
        if (prop is null || !prop.CanWrite || !prop.PropertyType.IsEnum) return;

        var values = Enum.GetValues(prop.PropertyType);
        if (values.Length == 0) return;

        var value = values.Length > 1 ? values.GetValue(1)! : values.GetValue(0)!;
        prop.SetValue(target, value);
    }

    private static void CopyActivityType(object source, object destination)
    {
        var sourceProp = source.GetType().GetProperty("ActivityType", BindingFlags.Instance | BindingFlags.Public);
        var destinationProp = destination.GetType().GetProperty("ActivityType", BindingFlags.Instance | BindingFlags.Public);

        if (sourceProp is null || destinationProp is null || !destinationProp.CanWrite) return;

        var value = sourceProp.GetValue(source);
        if (value is not null) destinationProp.SetValue(destination, value);
    }

    private static object? GetActivityTypeValue(object source)
    {
        var prop = source.GetType().GetProperty("ActivityType", BindingFlags.Instance | BindingFlags.Public);
        return prop?.GetValue(source);
    }

    private static double? ComputeExpectedCalories(float? userWeightKg, int durationMinutes, object? activityType)
    {
        if (userWeightKg is null || userWeightKg <= 0 || durationMinutes <= 0 || activityType is null) return null;

        var met = InvokeWorkoutMetCatalog(activityType);
        if (met <= 0) return null;

        var caloriesPerMinute = (met * 3.5 * userWeightKg.Value) / 200.0;
        return Math.Round(caloriesPerMinute * durationMinutes, 2);
    }

    private static double InvokeWorkoutMetCatalog(object activityType)
    {
        var catalogType = typeof(WorkoutService).Assembly
            .GetType("HealthingHand.Web.Services.WorkoutItems.WorkoutActivityMetCatalog")
            ?? throw new InvalidOperationException("Could not locate WorkoutActivityMetCatalog.");

        var method = catalogType.GetMethod("GetMet", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not locate WorkoutActivityMetCatalog.GetMet.");

        return Convert.ToDouble(method.Invoke(null, [activityType]));
    }
}