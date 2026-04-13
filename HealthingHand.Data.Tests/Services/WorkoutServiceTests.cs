using System.Reflection;
using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;
using HealthingHand.Data.Tests.Infrastructure;
using HealthingHand.Web.Services;
using HealthingHand.Web.Services.WorkoutItems;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using static HealthingHand.Data.Tests.Infrastructure.TestUserFactory;

namespace HealthingHand.Data.Tests.Services;

public class WorkoutServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task WorkoutService_AddWorkoutAsync_PersistsWorkoutAndMappedExercises()
    {
        await using var db = fixture.CreateDb();

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
        await using var db = fixture.CreateDb();

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
        await using var db = fixture.CreateDb();

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
        await using var db = fixture.CreateDb();

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
        await using var db = fixture.CreateDb();

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
        await using var db = fixture.CreateDb();

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

        var factory = fixture.Factory;
        return new WorkoutService(new WorkoutStore(factory), new WeightStore(factory), new HttpContextAccessor { HttpContext = httpContext });
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