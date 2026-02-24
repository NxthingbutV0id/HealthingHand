using HealthingHand.Data.Entries;

namespace HealthingHand.Data.Stores;

public interface ISleepStore
{
    Task AddAsync(SleepEntry entry, CancellationToken ct = default);
    Task<List<SleepEntry>> ListForUserAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default);
}