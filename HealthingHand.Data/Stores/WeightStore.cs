using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface IWeightStore : IStore<WeightEntry, int>
{
    Task<List<WeightEntry>> ListForUserAsync(Guid userId, DateTime from, DateTime to);
}

public class WeightStore(IDbContextFactory<AppDbContext> factory) : IWeightStore
{
    public async Task<WeightEntry?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WeightEntries.FindAsync(id);
    }

    public async Task<List<WeightEntry>> ListForUserAsync(Guid userId, DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();

        return await db.WeightEntries
            .Where(w => w.UserId == userId && w.Date >= from && w.Date <= to)
            .OrderByDescending(w => w.Date)
            .ToListAsync();
    }

    public async Task AddAsync(WeightEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.WeightEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(WeightEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.WeightEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.WeightEntries.FindAsync(id);
        if (entry is null) return;

        db.WeightEntries.Remove(entry);
        await db.SaveChangesAsync();
    }
}