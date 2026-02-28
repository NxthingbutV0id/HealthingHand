using System.Linq.Expressions;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface ISleepStore : IStore<SleepEntry, int>
{
    Task<List<SleepEntry>> ListForUserAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<SleepEntry?> GetForDateAsync(Guid userId, DateOnly date, CancellationToken ct = default);
}

public class SleepStore(IDbContextFactory<AppDbContext> factory) : ISleepStore
{
    public Task<SleepEntry?> GetAsync(int id)
    {
        return Task.Run(async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            return await db.SleepEntries.FindAsync(id);
        });
    }

    public async Task AddAsync(SleepEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.SleepEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public Task UpdateAsync(SleepEntry entry)
    {
        return Task.Run(async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            db.SleepEntries.Update(entry);
            await db.SaveChangesAsync();
        });
    }

    public async Task<List<SleepEntry>> ListForUserAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.SleepEntries
            .Where(s => s.UserId == userId && s.SleepDate >= from && s.SleepDate <= to)
            .OrderByDescending(s => s.SleepDate)
            .ToListAsync(ct);
    }
    
    public async Task<SleepEntry?> GetForDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.SleepEntries.SingleOrDefaultAsync(s => s.UserId == userId && s.SleepDate == date, ct);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.SleepEntries.FindAsync(id);
        if (entry is null) return;
        db.SleepEntries.Remove(entry);
        await db.SaveChangesAsync();
    }
}