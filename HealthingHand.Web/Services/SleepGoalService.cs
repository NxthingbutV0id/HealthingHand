using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.SleepItems;

namespace HealthingHand.Web.Services;

public interface ISleepGoalService
{
    Task<SleepGoalDto> GetGoalAsync();
    Task<(bool Success, string? Error)> SaveGoalAsync(SleepGoalInput input);
    IReadOnlyList<BedtimeRecommendation> CalculateRecommendations(TimeOnly desiredWakeTime, float preferredSleepHours);
}

public class SleepGoalService(
    ISleepGoalStore sleepGoals,
    IHttpContextAccessor httpContextAccessor) : ISleepGoalService
{
    private static readonly (int Cycles, float Hours)[] CycleOptions =
    [
        (6, 9.0f),
        (5, 7.5f),
        (4, 6.0f),
        (3, 4.5f)
    ];

    private static readonly TimeSpan FallAsleepDelay = TimeSpan.FromMinutes(15);
    private const int BestCycleCount = 5;

    public async Task<SleepGoalDto> GetGoalAsync()
    {
        var userId = GetCurrentUserId();
        var savedGoal = userId is null ? null : await sleepGoals.GetForUserAsync(userId.Value);

        var desiredWakeTime = savedGoal?.DesiredWakeTime ?? new TimeOnly(7, 0);
        var preferredSleepHours = savedGoal?.PreferredSleepHours ?? 7.5f;
        var recommendations = BuildRecommendations(desiredWakeTime, preferredSleepHours);
        var bestRecommendation = recommendations.First(recommendation => recommendation.IsBest);

        return new SleepGoalDto
        {
            Id = savedGoal?.Id ?? 0,
            DesiredWakeTime = desiredWakeTime,
            PreferredSleepHours = preferredSleepHours,
            BestRecommendedBedtime = savedGoal?.BestRecommendedBedtime ?? bestRecommendation.Bedtime,
            CreatedAt = savedGoal?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = savedGoal?.UpdatedAt ?? DateTime.UtcNow,
            Recommendations = recommendations
        };
    }

    public async Task<(bool Success, string? Error)> SaveGoalAsync(SleepGoalInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError);

        var recommendations = BuildRecommendations(input.DesiredWakeTime, input.PreferredSleepHours);
        var bestRecommendation = recommendations.First(recommendation => recommendation.IsBest);
        var existingGoal = await sleepGoals.GetForUserAsync(userId.Value);
        var now = DateTime.UtcNow;

        if (existingGoal is null)
        {
            await sleepGoals.AddAsync(new SleepGoalEntry
            {
                UserId = userId.Value,
                DesiredWakeTime = input.DesiredWakeTime,
                PreferredSleepHours = input.PreferredSleepHours,
                BestRecommendedBedtime = bestRecommendation.Bedtime,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existingGoal.DesiredWakeTime = input.DesiredWakeTime;
            existingGoal.PreferredSleepHours = input.PreferredSleepHours;
            existingGoal.BestRecommendedBedtime = bestRecommendation.Bedtime;
            existingGoal.UpdatedAt = now;

            await sleepGoals.UpdateAsync(existingGoal);
        }

        return (true, null);
    }

    public IReadOnlyList<BedtimeRecommendation> CalculateRecommendations(TimeOnly desiredWakeTime, float preferredSleepHours)
    {
        return BuildRecommendations(desiredWakeTime, preferredSleepHours);
    }

    private static IReadOnlyList<BedtimeRecommendation> BuildRecommendations(TimeOnly desiredWakeTime, float preferredSleepHours)
    {
        var wakeAnchor = DateTime.Today.Add(desiredWakeTime.ToTimeSpan());
        var closestHours = ClosestDistance(preferredSleepHours);

        return CycleOptions
            .Select(option =>
            {
                var recommendedBedtime = wakeAnchor - FallAsleepDelay - TimeSpan.FromHours(option.Hours);

                return new BedtimeRecommendation
                {
                    Cycles = option.Cycles,
                    SleepHours = option.Hours,
                    Bedtime = TimeOnly.FromDateTime(recommendedBedtime),
                    // Five cycles is a practical general-wellness default because it lands near the common
                    // 7-9 hour recommendation while still aligning with full sleep cycles.
                    IsBest = option.Cycles == BestCycleCount
                };
            })
            .OrderByDescending(option => option.Cycles)
            .Select(option =>
            {
                option.IsClosestToGoal = option.SleepHours == closestHours;
                return option;
            })
            .ToList();
    }

    private static float ClosestDistance(float preferredSleepHours)
    {
        return CycleOptions
            .Select(option => option.Hours)
            .OrderBy(hours => Math.Abs(hours - preferredSleepHours))
            .First();
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? Validate(SleepGoalInput input)
    {
        if (input.PreferredSleepHours < 4.5f || input.PreferredSleepHours > 12.0f)
            return "Preferred sleep hours must be between 4.5 and 12.";

        return null;
    }
}
