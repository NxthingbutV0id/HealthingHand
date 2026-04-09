using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface ISleepGoalStore : IStore<SleepGoalEntry, int>
{
    Task<SleepGoalEntry?> GetForUserAsync(Guid userId);
}

public class SleepGoalStore(IDbContextFactory<AppDbContext> factory) : ISleepGoalStore
{
    public async Task<SleepGoalEntry?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SleepGoals.FindAsync(id);
    }

    public async Task<SleepGoalEntry?> GetForUserAsync(Guid userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SleepGoals.SingleOrDefaultAsync(goal => goal.UserId == userId);
    }

    public async Task AddAsync(SleepGoalEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.SleepGoals.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SleepGoalEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.SleepGoals.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.SleepGoals.FindAsync(id);
        if (entry is null) return;

        db.SleepGoals.Remove(entry);
        await db.SaveChangesAsync();
    }
}
