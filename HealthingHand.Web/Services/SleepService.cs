using HealthingHand.Web.Services.SleepItems;
using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;

namespace HealthingHand.Web.Services;

public interface ISleepService
{
    Task<(bool Success, string? Error)> SaveAsync(SleepEntryInput input);
    Task<(bool Success, string? Error)> DeleteAsync(int id);
    Task<IReadOnlyList<SleepListItem>> ListHistoryAsync(DateOnly from, DateOnly to);
    Task<SleepSummaryDto> GetSummaryAsync(DateOnly from, DateOnly to);
    Task<IReadOnlyList<SleepTrendPoint>> GetTrendAsync(int days);
}

public class SleepService(ISleepStore sleeps, IHttpContextAccessor httpContextAccessor) : ISleepService
{
    public async Task<(bool Success, string? Error)> SaveAsync(SleepEntryInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError);

        var sleepDate = DateOnly.FromDateTime(input.StartTime);
        var storedQuality = EncodeSleepQuality(input.SleepQuality);

        var existing = await sleeps.GetForDateAsync(userId.Value, sleepDate);

        if (existing is null)
        {
            await sleeps.AddAsync(new SleepEntry
            {
                UserId = userId.Value,
                SleepDate = sleepDate,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                SleepQuality = storedQuality,
                Notes = input.Notes.Trim()
            });
        }
        else
        {
            existing.SleepDate = sleepDate;
            existing.StartTime = input.StartTime;
            existing.EndTime = input.EndTime;
            existing.SleepQuality = storedQuality;
            existing.Notes = input.Notes.Trim();

            await sleeps.UpdateAsync(existing);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var existing = await sleeps.GetAsync(id);
        if (existing is null || existing.UserId != userId.Value)
            return (false, "Sleep entry not found.");

        await sleeps.DeleteAsync(id);
        return (true, null);
    }

    public async Task<IReadOnlyList<SleepListItem>> ListHistoryAsync(DateOnly from, DateOnly to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        var entries = await sleeps.ListForUserAsync(userId.Value, from, to);

        return entries
            .OrderByDescending(s => s.SleepDate)
            .Select(ToListItem)
            .ToList();
    }

    public async Task<SleepSummaryDto> GetSummaryAsync(DateOnly from, DateOnly to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return new SleepSummaryDto
            {
                From = from,
                To = to
            };
        }

        var entries = await sleeps.ListForUserAsync(userId.Value, from, to);
        var items = entries
            .Select(ToListItem)
            .ToList();

        if (items.Count == 0)
        {
            return new SleepSummaryDto
            {
                From = from,
                To = to
            };
        }

        var totalHours = items.Sum(x => x.DurationHours);

        return new SleepSummaryDto
        {
            From = from,
            To = to,
            EntryCount = items.Count,
            TotalHours = totalHours,
            AverageHours = totalHours / items.Count,
            AverageSleepQuality = items.Average(x => x.SleepQuality),
            LongestNightHours = items.Max(x => x.DurationHours),
            ShortestNightHours = items.Min(x => x.DurationHours)
        };
    }

    public async Task<IReadOnlyList<SleepTrendPoint>> GetTrendAsync(int days)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        if (days <= 0)
            days = 7;

        var to = DateOnly.FromDateTime(DateTime.Today);
        var from = to.AddDays(-(days - 1));

        var entries = await sleeps.ListForUserAsync(userId.Value, from, to);

        return entries
            .Select(ToListItem)
            .OrderBy(x => x.SleepDate)
            .Select(x => new SleepTrendPoint
            {
                SleepDate = x.SleepDate,
                DurationHours = x.DurationHours,
                SleepQuality = x.SleepQuality
            })
            .ToList();
    }

    private static SleepListItem ToListItem(SleepEntry entry)
    {
        var duration = entry.EndTime - entry.StartTime;

        return new SleepListItem
        {
            Id = entry.Id,
            SleepDate = entry.SleepDate,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            SleepQuality = DecodeSleepQuality(entry.SleepQuality),
            Notes = entry.Notes,
            DurationHours = Math.Round(duration.TotalHours, 2)
        };
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? Validate(SleepEntryInput input)
    {
        if (input.StartTime == default)
            return "Start time is required.";

        if (input.EndTime == default)
            return "End time is required.";

        if (input.EndTime <= input.StartTime)
            return "End time must be after start time.";

        var duration = input.EndTime - input.StartTime;
        if (duration.TotalHours > 24)
            return "Sleep duration cannot exceed 24 hours.";

        return input.SleepQuality is < 0 or > 5 ? "Sleep quality must be between 0 and 5." : null;
    }

    private static byte EncodeSleepQuality(double quality)
    {
        var clamped = Math.Clamp(quality, 0.0, 5.0);
        return (byte)Math.Round(clamped * 32.0);
    }

    private static float DecodeSleepQuality(byte storedQuality)
    {
        return storedQuality / 32.0f;
    }
}