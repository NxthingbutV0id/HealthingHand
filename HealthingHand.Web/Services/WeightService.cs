using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.WeightItems;

namespace HealthingHand.Web.Services;

public interface IWeightService
{
    Task<(bool Success, string? Error)> SaveAsync(WeightEntryInput input);
    Task<IReadOnlyList<WeightListItem>> ListHistoryAsync(DateTime from, DateTime to);
    Task<IReadOnlyList<WeightTrendPoint>> GetTrendAsync(int days);
}

public class WeightService(IWeightStore weights, IHttpContextAccessor httpContextAccessor) : IWeightService
{
    public async Task<(bool Success, string? Error)> SaveAsync(WeightEntryInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError);

        var normalizedDate = input.Date.Date;
        var existing = await weights.GetForDateAsync(userId.Value, normalizedDate);

        if (existing is null)
        {
            await weights.AddAsync(new WeightEntry
            {
                UserId = userId.Value,
                Date = normalizedDate,
                WeightKg = input.WeightKg
            });
        }
        else
        {
            existing.Date = normalizedDate;
            existing.WeightKg = input.WeightKg;
            await weights.UpdateAsync(existing);
        }

        return (true, null);
    }

    public async Task<IReadOnlyList<WeightListItem>> ListHistoryAsync(DateTime from, DateTime to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        var entries = await weights.ListForUserAsync(userId.Value, from.Date, to.Date);

        return entries
            .OrderByDescending(w => w.Date)
            .Select(w => new WeightListItem
            {
                Id = w.Id,
                Date = w.Date,
                WeightKg = w.WeightKg
            })
            .ToList();
    }

    public async Task<IReadOnlyList<WeightTrendPoint>> GetTrendAsync(int days)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        if (days <= 0)
            days = 14;

        var to = DateTime.Today;
        var from = to.AddDays(-(days - 1));

        var entries = await weights.ListForUserAsync(userId.Value, from, to);

        return entries
            .OrderBy(w => w.Date)
            .Select(w => new WeightTrendPoint
            {
                Date = w.Date,
                WeightKg = w.WeightKg
            })
            .ToList();
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? Validate(WeightEntryInput input)
    {
        if (input.Date == default)
            return "Weight date is required.";

        if (input.WeightKg <= 0 || input.WeightKg > 1000)
            return "Weight must be between 0 and 1000 kg.";

        return null;
    }
}
