using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data;

public interface IDatabase
{
    IAccountStore Account { get; }
    ISleepStore Sleep { get; }
    ISleepGoalStore SleepGoals { get; }
    IDietStore Diet { get; }
    IWorkoutStore Workout { get; }
    IWeightStore Weight { get; }
    IWeightGoalStore WeightGoals { get; }

    Task InitializeAsync();
}

public class Database(
    IDbContextFactory<AppDbContext> dbContext,
    IAccountStore account,
    ISleepStore sleep,
    ISleepGoalStore sleepGoals,
    IWorkoutStore workout,
    IDietStore diet,
    IWeightStore weight,
    IWeightGoalStore weightGoals)
    : IDatabase
{
    public IAccountStore Account { get; } = account;
    public ISleepStore Sleep { get; } = sleep;
    public ISleepGoalStore SleepGoals { get; } = sleepGoals;
    public IDietStore Diet { get; } = diet;
    public IWorkoutStore Workout { get; } = workout;
    public IWeightStore Weight { get; } = weight;
    public IWeightGoalStore WeightGoals { get; } = weightGoals;

    public async Task InitializeAsync()
    {
        await using var db = await dbContext.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

}