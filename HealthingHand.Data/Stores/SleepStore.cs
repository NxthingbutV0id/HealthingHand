using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public class SleepStore : ISleepStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public SleepStore(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task AddAsync(SleepEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.SleepEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SleepEntry>> ListForUserAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.SleepEntries
            .Where(s => s.UserId == userId && s.Date >= from && s.Date <= to)
            .OrderByDescending(s => s.Date)
            .ToListAsync(ct);
    }
}