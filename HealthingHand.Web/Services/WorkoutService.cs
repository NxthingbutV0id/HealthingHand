using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Web.Services.WorkoutItems;

namespace HealthingHand.Web.Services;

public interface IWorkoutService
{
    Task<(bool Success, string? Error, int? WorkoutId)> AddWorkoutAsync(WorkoutEntryInput input);
    Task<IReadOnlyList<WorkoutListItem>> ListPastWorkoutsAsync(DateTime from, DateTime to);
    Task<(bool Success, string? Error, WorkoutDetailDto? Workout)> GetWorkoutAsync(int id);
    Task<(bool Success, string? Error)> UpdateWorkoutAsync(int id, WorkoutEntryInput input);
    Task<(bool Success, string? Error)> DeleteWorkoutAsync(int id);
}

public class WorkoutService(
    IWorkoutStore workouts,
    IWeightStore weights,
    IHttpContextAccessor httpContextAccessor) : IWorkoutService
{
    public async Task<(bool Success, string? Error, int? WorkoutId)> AddWorkoutAsync(WorkoutEntryInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.", null);

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError, null);

        var workout = MapToEntry(input, userId.Value);
        await workouts.AddAsync(workout);

        return (true, null, workout.Id);
    }

    public async Task<IReadOnlyList<WorkoutListItem>> ListPastWorkoutsAsync(DateTime from, DateTime to)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return [];

        var userWeightKg = await GetLatestWeightAsync(userId.Value);
        var entries = await workouts.ListForUserAsync(userId.Value, from, to);

        return entries
            .OrderByDescending(w => w.StartedAt)
            .Select(w => ToListItem(w, userWeightKg))
            .ToList();
    }

    public async Task<(bool Success, string? Error, WorkoutDetailDto? Workout)> GetWorkoutAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.", null);

        var userWeightKg = await GetLatestWeightAsync(userId.Value);
        var workout = await workouts.GetWithExercisesAsync(id);
        if (workout is null || workout.UserId != userId.Value)
            return (false, "Workout not found.", null);

        return (true, null, ToDetail(workout, userWeightKg));
    }

    public async Task<(bool Success, string? Error)> UpdateWorkoutAsync(int id, WorkoutEntryInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var validationError = Validate(input);
        if (validationError is not null)
            return (false, validationError);

        var existing = await workouts.GetWithExercisesAsync(id);
        if (existing is null || existing.UserId != userId.Value)
            return (false, "Workout not found.");

        if (HasExerciseChanges(existing.Exercises, input.Exercises))
        {
            return (false, "Updating exercises is not supported yet. Keep the existing exercises unchanged or recreate the workout.");
        }

        existing.StartedAt = input.StartedAt;
        existing.DurationMinutes = input.DurationMinutes;
        existing.WorkoutType = input.WorkoutType;
        existing.SelfReportedIntensity = (byte)input.SelfReportedIntensity;
        existing.AverageHeartRate = input.AverageHeartRate;
        existing.Notes = input.Notes.Trim();

        await workouts.UpdateAsync(existing);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteWorkoutAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return (false, "Not signed in.");

        var existing = await workouts.GetAsync(id);
        if (existing is null || existing.UserId != userId.Value)
            return (false, "Workout not found.");

        await workouts.DeleteAsync(id);
        return (true, null);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? Validate(WorkoutEntryInput input)
    {
        var now = DateTime.Now;

        if (input.StartedAt == default)
            return "Workout start time is required.";

        if (input.StartedAt > now)
            return "Workout entries cannot start in the future.";

        if (input.DurationMinutes <= 0 || input.DurationMinutes > 600)
            return "Workout duration must be between 1 and 600 minutes.";

        if (input.StartedAt.AddMinutes(input.DurationMinutes) > now)
            return "Workout entries cannot end in the future.";

        if (input.SelfReportedIntensity is < 0 or > 5)
            return "Intensity must be between 0 and 5.";

        if (input.AverageHeartRate is < 30 or > 240)
            return "Average heart rate must be between 30 and 240 bpm.";

        for (var i = 0; i < input.Exercises.Count; i++)
        {
            var exercise = input.Exercises[i];
            var row = i + 1;

            if (string.IsNullOrWhiteSpace(exercise.Name))
                continue;

            if (exercise.Sets < 0 || exercise.Reps < 0 || exercise.WeightKg < 0 || exercise.DistanceKm < 0 || exercise.DurationMinutes < 0)
                return $"Exercise #{row} cannot contain negative values.";
        }

        return null;
    }

    private static WorkoutEntry MapToEntry(WorkoutEntryInput input, Guid userId)
    {
        return new WorkoutEntry
        {
            UserId = userId,
            StartedAt = input.StartedAt,
            DurationMinutes = input.DurationMinutes,
            WorkoutType = input.WorkoutType,
            SelfReportedIntensity = (byte)input.SelfReportedIntensity,
            AverageHeartRate = input.AverageHeartRate,
            Notes = input.Notes.Trim(),
            Exercises = input.Exercises
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .Select(e => new ExerciseEntry
                {
                    Name = e.Name.Trim(),
                    ActivityType = e.ActivityType,
                    Sets = e.Sets,
                    Reps = e.Reps,
                    WeightKg = e.WeightKg,
                    DistanceKm = e.DistanceKm,
                    Time = TimeSpan.FromMinutes(e.DurationMinutes)
                })
                .ToList()
        };
    }

    private static WorkoutListItem ToListItem(WorkoutEntry entry, float? userWeightKg)
    {
        var estimatedCaloriesBurned = CalculateWorkoutCaloriesBurned(entry.Exercises, userWeightKg);

        return new WorkoutListItem
        {
            Id = entry.Id,
            StartedAt = entry.StartedAt,
            DurationMinutes = entry.DurationMinutes,
            WorkoutType = entry.WorkoutType,
            SelfReportedIntensity = entry.SelfReportedIntensity,
            AverageHeartRate = entry.AverageHeartRate,
            EstimatedCaloriesBurned = estimatedCaloriesBurned,
            Notes = entry.Notes,
            ExerciseCount = entry.Exercises.Count
        };
    }

    private static WorkoutDetailDto ToDetail(WorkoutEntry entry, float? userWeightKg)
    {
        var exerciseItems = entry.Exercises
            .Select(e =>
            {
                var durationMinutes = (int)Math.Round(e.Time.TotalMinutes);
                var met = WorkoutActivityMetCatalog.GetMet(e.ActivityType);

                return new WorkoutExerciseItem
                {
                    Name = e.Name,
                    ActivityType = e.ActivityType,
                    Met = met,
                    EstimatedCaloriesBurned = CalculateExerciseCaloriesBurned(met, userWeightKg, durationMinutes),
                    Sets = e.Sets,
                    Reps = e.Reps,
                    WeightKg = e.WeightKg,
                    DistanceKm = e.DistanceKm,
                    DurationMinutes = durationMinutes
                };
            })
            .ToList();

        return new WorkoutDetailDto
        {
            Id = entry.Id,
            StartedAt = entry.StartedAt,
            DurationMinutes = entry.DurationMinutes,
            WorkoutType = entry.WorkoutType,
            SelfReportedIntensity = entry.SelfReportedIntensity,
            AverageHeartRate = entry.AverageHeartRate,
            UserWeightKg = userWeightKg,
            EstimatedCaloriesBurned = exerciseItems.SumCalories(),
            Notes = entry.Notes,
            Exercises = exerciseItems
        };
    }

    private static bool HasExerciseChanges(IReadOnlyList<ExerciseEntry> existing, IReadOnlyList<WorkoutExerciseInput> requested)
    {
        var existingNormalized = existing
            .Select(e => new
            {
                Name = e.Name.Trim(),
                e.ActivityType,
                e.Sets,
                e.Reps,
                e.WeightKg,
                e.DistanceKm,
                DurationMinutes = (int)Math.Round(e.Time.TotalMinutes)
            })
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ActivityType)
            .ThenBy(e => e.Sets)
            .ThenBy(e => e.Reps)
            .ThenBy(e => e.WeightKg)
            .ThenBy(e => e.DistanceKm)
            .ThenBy(e => e.DurationMinutes)
            .ToList();

        var requestedNormalized = requested
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => new
            {
                Name = e.Name.Trim(),
                e.ActivityType,
                e.Sets,
                e.Reps,
                e.WeightKg,
                e.DistanceKm,
                e.DurationMinutes
            })
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ActivityType)
            .ThenBy(e => e.Sets)
            .ThenBy(e => e.Reps)
            .ThenBy(e => e.WeightKg)
            .ThenBy(e => e.DistanceKm)
            .ThenBy(e => e.DurationMinutes)
            .ToList();

        if (existingNormalized.Count != requestedNormalized.Count)
            return true;

        for (var i = 0; i < existingNormalized.Count; i++)
        {
            if (!string.Equals(existingNormalized[i].Name, requestedNormalized[i].Name, StringComparison.OrdinalIgnoreCase) ||
                existingNormalized[i].ActivityType != requestedNormalized[i].ActivityType ||
                existingNormalized[i].Sets != requestedNormalized[i].Sets ||
                existingNormalized[i].Reps != requestedNormalized[i].Reps ||
                existingNormalized[i].WeightKg != requestedNormalized[i].WeightKg ||
                existingNormalized[i].DistanceKm != requestedNormalized[i].DistanceKm ||
                existingNormalized[i].DurationMinutes != requestedNormalized[i].DurationMinutes)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<float?> GetLatestWeightAsync(Guid userId)
    {
        var entries = await weights.ListForUserAsync(userId, DateTime.MinValue, DateTime.MaxValue);
        return entries.FirstOrDefault()?.WeightKg;
    }

    private static double? CalculateWorkoutCaloriesBurned(IEnumerable<ExerciseEntry> exercises, float? userWeightKg)
    {
        if (userWeightKg is null || userWeightKg <= 0)
            return null;

        var total = exercises.Sum(exercise =>
            CalculateExerciseCaloriesBurned(
                WorkoutActivityMetCatalog.GetMet(exercise.ActivityType),
                userWeightKg,
                (int)Math.Round(exercise.Time.TotalMinutes)) ?? 0);

        return Math.Round(total, 2);
    }

    private static double? CalculateExerciseCaloriesBurned(double met, float? userWeightKg, int durationMinutes)
    {
        if (userWeightKg is null || userWeightKg <= 0 || durationMinutes <= 0 || met <= 0)
            return null;

        var caloriesPerMinute = (met * 3.5 * userWeightKg.Value) / 200.0;
        return Math.Round(caloriesPerMinute * durationMinutes, 2);
    }
}

file static class WorkoutCaloriesExtensions
{
    public static double? SumCalories(this IEnumerable<WorkoutExerciseItem> exercises)
    {
        var calories = exercises
            .Where(e => e.EstimatedCaloriesBurned.HasValue)
            .Select(e => e.EstimatedCaloriesBurned!.Value)
            .ToList();

        return calories.Count == 0 ? null : Math.Round(calories.Sum(), 2);
    }
}
