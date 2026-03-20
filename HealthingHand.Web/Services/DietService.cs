using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.DietItems;

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
                Calories = i.Calories,
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

        var mealCount = meals.Count;
        var itemCount = meals.Sum(m => m.Items.Count);
        var totalCalories = meals.Sum(m => m.Items.Sum(i => i.Calories));
        var totalProtein = meals.Sum(m => m.Items.Sum(i => i.ProteinGrams));
        var totalCarbs = meals.Sum(m => m.Items.Sum(i => i.CarbsGrams));
        var totalFat = meals.Sum(m => m.Items.Sum(i => i.FatGrams));

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
            AverageCaloriesPerMeal = mealCount == 0 ? 0 : (double)totalCalories / mealCount
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
            if (item.Calories < 0) return $"Food item #{row} cannot have negative calories.";
            if (item.ProteinGrams < 0 || item.CarbsGrams < 0 || item.FatGrams < 0) 
                return $"Food item #{row} cannot have negative macro values.";
        }

        return null;
    }
}