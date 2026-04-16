using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface IWeightGoalStore : IStore<WeightGoalEntry, int>
{
    Task<WeightGoalEntry?> GetForUserAsync(Guid userId);
}

public class WeightGoalStore(IDbContextFactory<AppDbContext> factory) : IWeightGoalStore
{
    public async Task<WeightGoalEntry?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WeightGoals.FindAsync(id);
    }

    public async Task<WeightGoalEntry?> GetForUserAsync(Guid userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WeightGoals.SingleOrDefaultAsync(goal => goal.UserId == userId);
    }

    public async Task AddAsync(WeightGoalEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.WeightGoals.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(WeightGoalEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.WeightGoals.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.WeightGoals.FindAsync(id);
        if (entry is null) return;

        db.WeightGoals.Remove(entry);
        await db.SaveChangesAsync();
    }
}
