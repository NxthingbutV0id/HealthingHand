using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data;

public interface IDatabase
{
    IAccountStore Account { get; }
    ISleepStore Sleep { get; }
    IDietStore Diet { get; }
    IWorkoutStore Workout { get; }
    IWeightStore Weight { get; }

    Task InitializeAsync();
}

public class Database(
    IDbContextFactory<AppDbContext> dbContext,
    IAccountStore account,
    ISleepStore sleep,
    IWorkoutStore workout,
    IDietStore diet,
    IWeightStore weight)
    : IDatabase
{
    public IAccountStore Account { get; } = account;
    public ISleepStore Sleep { get; } = sleep;
    public IDietStore Diet { get; } = diet;
    public IWorkoutStore Workout { get; } = workout;
    public IWeightStore Weight { get; } = weight;

    public async Task InitializeAsync()
    {
        await using var db = await dbContext.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

}