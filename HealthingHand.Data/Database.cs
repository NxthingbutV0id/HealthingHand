using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data;

public class Database(
    IDbContextFactory<AppDbContext> dbContext,
    AccountStore account,
    SleepStore sleep,
    WorkoutStore workout,
    DietStore diet)
    : IDatabase
{
    public AccountStore Account { get; } = account;
    public SleepStore Sleep { get; } = sleep;
    public DietStore Diet { get; } = diet;
    public WorkoutStore Workout { get; } = workout;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await dbContext.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

}