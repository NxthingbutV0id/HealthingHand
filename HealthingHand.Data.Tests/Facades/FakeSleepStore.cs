using HealthingHand.Data.Entries;
using HealthingHand.Data.Stores;

namespace HealthingHand.Data.Tests;

public sealed class FakeSleepStore : ISleepStore
{
    private readonly List<SleepEntry> _entries = [];
    private int _nextId = 1;

    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public IReadOnlyList<SleepEntry> Entries => _entries;

    public void Seed(params SleepEntry[] entries)
    {
        foreach (var entry in entries)
        {
            var copy = Clone(entry);

            if (copy.Id <= 0) copy.Id = _nextId++;
            else _nextId = Math.Max(_nextId, copy.Id + 1);

            _entries.Add(copy);
        }
    }

    public Task<SleepEntry?> GetAsync(int id)
    {
        var found = _entries
            .Where(e => e.Id == id)
            .Select(Clone)
            .SingleOrDefault();

        return Task.FromResult(found);
    }

    public Task AddAsync(SleepEntry entry)
    {
        AddCalls++;

        var copy = Clone(entry);
        copy.Id = _nextId++;
        _entries.Add(copy);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SleepEntry entry)
    {
        UpdateCalls++;

        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0) _entries[index] = Clone(entry);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        DeleteCalls++;
        _entries.RemoveAll(e => e.Id == id);
        return Task.CompletedTask;
    }

    public Task<List<SleepEntry>> ListForUserAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var results = _entries
            .Where(e => e.UserId == userId &&
                        e.SleepDate >= from &&
                        e.SleepDate <= to)
            .Select(Clone)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<SleepEntry?> GetForDateAsync(Guid userId, DateOnly date)
    {
        var found = _entries
            .Where(e => e.UserId == userId && e.SleepDate == date)
            .Select(Clone)
            .SingleOrDefault();

        return Task.FromResult(found);
    }

    private static SleepEntry Clone(SleepEntry entry)
    {
        return new SleepEntry
        {
            Id = entry.Id,
            UserId = entry.UserId,
            User = entry.User,
            SleepDate = entry.SleepDate,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            SleepQuality = entry.SleepQuality,
            Notes = entry.Notes
        };
    }
}