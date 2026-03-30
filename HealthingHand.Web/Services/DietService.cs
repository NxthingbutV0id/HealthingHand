using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.DietItems;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Web.Services;

public interface IDietService
{
    Task<(bool Success, string? Error, int? MealId)> CreateMealAsync(DietMealInput input);
    Task<IReadOnlyList<DietMealListItem>> ListMealsAsync(DateTime from, DateTime to);
    Task<DietSummaryDto> GetSummaryAsync(DateTime from, DateTime to);
    Task<DietSummaryDto> GetTodaySummaryAsync();
}

public class DietService(
    IDietStore diets,
    IAccountStore accounts,
    IDbContextFactory<AppDbContext> dbFactory,
    IHttpContextAccessor httpContextAccessor) : IDietService
{
    public async Task<(bool Success, string? Error, int? MealId)> CreateMealAsync(DietMealInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.", null);

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError, null);

        var meal = new DietEntry
        {
            UserId = userId.Value,
            EatenAt = input.EatenAt == default ? DateTime.Now : input.EatenAt,
            MealType = input.MealType.Trim(),
            Notes = input.Notes.Trim()
        };

        var items = input.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new MealItemEntry
            {
                Name = i.Name.Trim(),
                Quantity = i.Quantity,
                Unit = i.Unit.Trim(),
                Calories = (int)(i.CaloriesPerUnit * i.Quantity),
                ProteinGrams = i.ProteinGrams,
                CarbsGrams = i.CarbsGrams,
                FatGrams = i.FatGrams
            })
            .ToList();

        var mealId = await diets.AddWithItemsAsync(meal, items);
        return (true, null, mealId);
    }

    public async Task<IReadOnlyList<DietMealListItem>> ListMealsAsync(DateTime from, DateTime to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        var meals = await diets.ListForUserAsync(userId.Value, from, to, includeItems: true);

        return meals
            .Select(m => new DietMealListItem
            {
                Id = m.Id,
                EatenAt = m.EatenAt,
                MealType = m.MealType,
                Notes = m.Notes,
                ItemCount = m.Items.Count,
                TotalCalories = m.Items.Sum(i => i.Calories),
                TotalProteinGrams = m.Items.Sum(i => i.ProteinGrams),
                TotalCarbsGrams = m.Items.Sum(i => i.CarbsGrams),
                TotalFatGrams = m.Items.Sum(i => i.FatGrams)
            })
            .ToList();
    }

    public async Task<DietSummaryDto> GetSummaryAsync(DateTime from, DateTime to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return new DietSummaryDto
            {
                From = from,
                To = to
            };
        }

        var meals = await diets.ListForUserAsync(userId.Value, from, to, includeItems: true);
        var user = await accounts.GetAsync(userId.Value);

        float? latestWeightKg;

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            latestWeightKg = await db.WeightEntries
                .Where(w => w.UserId == userId.Value)
                .OrderByDescending(w => w.Date)
                .Select(w => (float?)w.WeightKg)
                .FirstOrDefaultAsync();
        }

        var mealCount = meals.Count;
        var itemCount = meals.Sum(m => m.Items.Count);
        var totalCalories = meals.Sum(m => m.Items.Sum(i => i.Calories));
        var totalProtein = meals.Sum(m => m.Items.Sum(i => i.ProteinGrams));
        var totalCarbs = meals.Sum(m => m.Items.Sum(i => i.CarbsGrams));
        var totalFat = meals.Sum(m => m.Items.Sum(i => i.FatGrams));

        var target = CalculateDailyCalorieTarget(user, latestWeightKg);

        return new DietSummaryDto
        {
            From = from,
            To = to,
            MealCount = mealCount,
            ItemCount = itemCount,
            TotalCalories = totalCalories,
            TotalProteinGrams = totalProtein,
            TotalCarbsGrams = totalCarbs,
            TotalFatGrams = totalFat,
            AverageCaloriesPerMeal = mealCount == 0 ? 0 : (double)totalCalories / mealCount,
            CaloriesDelta = target is null ? 0 : totalCalories - target.Value,
            DailyCalorieTarget = target,
            CaloriesRemaining = target - totalCalories,
            TargetMethodDescription = target is null
                ? "Target unavailable until profile and weight data exist."
                : "Derived from latest weight entry plus profile data using Mifflin-St Jeor with sedentary factor 1.2."
        };
    }

    public Task<DietSummaryDto> GetTodaySummaryAsync()
    {
        var from = DateTime.Today;
        var to = from.AddDays(1).AddTicks(-1);
        return GetSummaryAsync(from, to);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? Validate(DietMealInput input)
    {
        if (input.EatenAt == default) return "Meal date/time is required.";
        if (string.IsNullOrWhiteSpace(input.MealType)) return "Meal type is required.";
        if (input.Items.Count == 0) return "Add at least one food item.";

        for (var i = 0; i < input.Items.Count; i++)
        {
            var item = input.Items[i];
            var row = i + 1;

            if (string.IsNullOrWhiteSpace(item.Name)) return $"Food item #{row} must have a name.";
            if (item.Quantity <= 0) return $"Food item #{row} must have a quantity greater than 0.";
            if (string.IsNullOrWhiteSpace(item.Unit)) return $"Food item #{row} must have a unit.";
            if (item.CaloriesPerUnit < 0) return $"Food item #{row} cannot have negative calories.";
            if (item.ProteinGrams < 0 || item.CarbsGrams < 0 || item.FatGrams < 0) 
                return $"Food item #{row} cannot have negative macro values.";
        }

        return null;
    }
    
    private static int? CalculateDailyCalorieTarget(UserEntry? user, float? weightKg)
    {
        if (user is null || weightKg is null || user.HeightM <= 0) return null;
        var heightCm = user.HeightM * 100f;
        var age = user.Age;

        // weightKg should not be null beyond this point, but C# is being dumb...
        var bmr = 10 * weightKg + 6.25 * heightCm - 5 * age + (user.Sex == Sex.Male ? 5 : -161);
        
        return (int)Math.Round((bmr ?? 0) * 1.2);
    }
}