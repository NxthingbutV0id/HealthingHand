using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using HealthingHand.Data.Tests.Infrastructure;
using HealthingHand.Web.Services;
using HealthingHand.Web.Services.DietItems;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using static HealthingHand.Data.Tests.Infrastructure.TestUserFactory;


namespace HealthingHand.Data.Tests.Services;

public class DietServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task CreateMealAsync_PersistsMealAndComputedItems()
    {
        await using var db = fixture.CreateDb();

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
    public async Task ListMealsAsync_ReturnsOnlyCurrentUsersMealsWithAggregates()
    {
        await using var db = fixture.CreateDb();

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
    public async Task GetSummaryAsync_ReturnsTotalsAndDerivedTarget()
    {
        await using var db = fixture.CreateDb();

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
    public async Task GetSummaryAsync_RefreshesAgainstLatestDatabaseState()
    {
        await using var db = fixture.CreateDb();

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

        var factory = fixture.Factory;
        return new DietService(new DietStore(factory), new AccountStore(factory), factory, new HttpContextAccessor { HttpContext = httpContext });
    }
}