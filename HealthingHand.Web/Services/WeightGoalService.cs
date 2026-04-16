using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.WeightItems;

namespace HealthingHand.Web.Services;

public interface IWeightGoalService
{
    Task<WeightGoalDto> GetGoalAsync();
    Task<(bool Success, string? Error)> SaveGoalAsync(WeightGoalInput input);
}

public class WeightGoalService(
    IWeightGoalStore weightGoals,
    IWeightStore weights,
    IHttpContextAccessor httpContextAccessor) : IWeightGoalService
{
    public async Task<WeightGoalDto> GetGoalAsync()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return new WeightGoalDto
            {
                GoalWeightKg = 0,
                CurrentWeightKg = 0,
                RecommendedDirection = "Sign in to save a weight goal."
            };
        }

        var existingGoal = await weightGoals.GetForUserAsync(userId.Value);
        if (existingGoal is not null)
        {
            return ToDto(existingGoal);
        }

        var latestWeight = await weights.GetLatestForUserAsync(userId.Value);
        var startingWeight = latestWeight?.WeightKg ?? 0;

        return new WeightGoalDto
        {
            CurrentWeightKg = startingWeight,
            GoalWeightKg = startingWeight,
            GoalType = WeightGoalType.MaintainWeight,
            PacePreference = GoalPacePreference.Moderate,
            ExerciseFrequency = ExerciseFrequency.Moderate,
            ExerciseIntensity = ExerciseIntensity.Medium,
            RecommendedDirection = BuildDirectionText(WeightGoalType.MaintainWeight, 0)
        };
    }

    public async Task<(bool Success, string? Error)> SaveGoalAsync(WeightGoalInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError);

        var normalizedGoalType = NormalizeGoalType(input.CurrentWeightKg, input.GoalWeightKg, input.GoalType);
        var existingGoal = await weightGoals.GetForUserAsync(userId.Value);
        var now = DateTime.UtcNow;

        if (existingGoal is null)
        {
            await weightGoals.AddAsync(new WeightGoalEntry
            {
                UserId = userId.Value,
                CurrentWeightKg = input.CurrentWeightKg,
                GoalWeightKg = input.GoalWeightKg,
                GoalType = normalizedGoalType,
                PacePreference = input.PacePreference,
                ExerciseFrequency = input.ExerciseFrequency.ToString(),
                ExerciseIntensity = input.ExerciseIntensity.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existingGoal.CurrentWeightKg = input.CurrentWeightKg;
            existingGoal.GoalWeightKg = input.GoalWeightKg;
            existingGoal.GoalType = normalizedGoalType;
            existingGoal.PacePreference = input.PacePreference;
            existingGoal.ExerciseFrequency = input.ExerciseFrequency.ToString();
            existingGoal.ExerciseIntensity = input.ExerciseIntensity.ToString();
            existingGoal.UpdatedAt = now;

            await weightGoals.UpdateAsync(existingGoal);
        }

        return (true, null);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static WeightGoalDto ToDto(WeightGoalEntry entry)
    {
        var difference = Math.Round(entry.GoalWeightKg - entry.CurrentWeightKg, 2);

        return new WeightGoalDto
        {
            Id = entry.Id,
            CurrentWeightKg = entry.CurrentWeightKg,
            GoalWeightKg = entry.GoalWeightKg,
            DifferenceKg = (float)difference,
            GoalType = entry.GoalType,
            PacePreference = entry.PacePreference,
            ExerciseFrequency = ParseExerciseFrequency(entry.ExerciseFrequency),
            ExerciseIntensity = ParseExerciseIntensity(entry.ExerciseIntensity),
            RecommendedDirection = BuildDirectionText(entry.GoalType, difference),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }

    private static string BuildDirectionText(WeightGoalType goalType, double differenceKg)
    {
        return goalType switch
        {
            WeightGoalType.LoseWeight => $"Lose {Math.Abs(differenceKg):0.##} kg over time.",
            WeightGoalType.GainWeight => $"Gain {Math.Abs(differenceKg):0.##} kg over time.",
            _ => "Maintain your current weight."
        };
    }

    private static WeightGoalType NormalizeGoalType(float currentWeightKg, float goalWeightKg, WeightGoalType selectedGoalType)
    {
        var difference = Math.Round(goalWeightKg - currentWeightKg, 2);

        if (Math.Abs(difference) < 0.01f)
            return WeightGoalType.MaintainWeight;

        return difference < 0
            ? WeightGoalType.LoseWeight
            : difference > 0
                ? WeightGoalType.GainWeight
                : selectedGoalType;
    }

    private static string? Validate(WeightGoalInput input)
    {
        if (input.CurrentWeightKg <= 0 || input.CurrentWeightKg > 1000)
            return "Current weight must be between 0 and 1000 kg.";

        if (input.GoalWeightKg <= 0 || input.GoalWeightKg > 1000)
            return "Goal weight must be between 0 and 1000 kg.";

        return null;
    }

    private static ExerciseFrequency ParseExerciseFrequency(string? value)
    {
        return Enum.TryParse<ExerciseFrequency>(value, out var parsed)
            ? parsed
            : ExerciseFrequency.Moderate;
    }

    private static ExerciseIntensity ParseExerciseIntensity(string? value)
    {
        return Enum.TryParse<ExerciseIntensity>(value, out var parsed)
            ? parsed
            : ExerciseIntensity.Medium;
    }
}
