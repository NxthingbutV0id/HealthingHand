using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data;

public class Database : IDatabase
{
    private readonly IDbContextFactory<AppDbContext> _dbContext;
    public IAccountStore Account { get; }
    public ISleepStore Sleep { get; }
    public IDietStore Diet { get; }
    public IWorkoutStore Workout { get; }
    
    public Database(IDbContextFactory<AppDbContext> dbContext, IAccountStore account, ISleepStore sleep, IWorkoutStore workout, IDietStore diet) 
    {
        _dbContext = dbContext;
        Sleep = sleep;
        Workout = workout;
        Diet = diet;
        Account = account;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContext.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

}