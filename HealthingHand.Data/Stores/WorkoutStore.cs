using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface IWorkoutStore : IStore<WorkoutEntry, int>
{
    Task<List<WorkoutEntry>> ListForUserAsync(Guid userId, DateTime from, DateTime to);
    Task<WorkoutEntry?> GetWithExercisesAsync(int id);
}

public class WorkoutStore(IDbContextFactory<AppDbContext> factory) : IWorkoutStore
{
    public async Task<WorkoutEntry?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WorkoutEntries.FindAsync(id);
    }

    public async Task<WorkoutEntry?> GetWithExercisesAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        return await db.WorkoutEntries
            .Include(w => w.Exercises)
            .SingleOrDefaultAsync(w => w.Id == id);
    }

    public async Task<List<WorkoutEntry>> ListForUserAsync(Guid userId, DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();

        return await db.WorkoutEntries
            .Where(w => w.UserId == userId && w.StartedAt >= from && w.StartedAt <= to)
            .OrderByDescending(w => w.StartedAt)
            .Include(w => w.Exercises) // remove if you don’t want exercises in list views
            .ToListAsync();
    }

    public async Task AddAsync(WorkoutEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Make sure children point to parent (helps EF if WorkoutId isn’t set)
        foreach (var ex in entry.Exercises)
            ex.WorkoutEntry = entry;

        db.WorkoutEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(WorkoutEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();

        // NOTE: This updates the workout row (scalar fields).
        // If you also want to update the Exercises collection, see note below.
        db.WorkoutEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var workout = await db.WorkoutEntries.FindAsync(id);
        if (workout is null) return;

        db.WorkoutEntries.Remove(workout);
        await db.SaveChangesAsync(); // exercises cascade-delete if you added OnDelete(Cascade)
    }
}