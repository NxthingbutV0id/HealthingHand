using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using HealthingHand.Data.Tests.Infrastructure;

using static HealthingHand.Data.Tests.Infrastructure.TestUserFactory;

namespace HealthingHand.Data.Tests.Stores;

public class DietStoreTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    /// <summary>
    /// This test verifies that the DietStore's AddWithItemsAsync method correctly saves a DietEntry
    /// along with its associated MealItemEntries to the database.
    /// </summary>
    [Fact]
    public async Task AddWithItemsAsync_PersistsMealAndItems()
    {
        await using var db = fixture.CreateDb();

        var user = MakeUser("diet1@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var store = new DietStore(fixture.Factory);

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
        await using var db = fixture.CreateDb();

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

        var store = new DietStore(fixture.Factory);

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
        var store = new DietStore(fixture.Factory);

        await using var db = fixture.CreateDb();
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
}