using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface IDietStore : IStore<DietEntry, int>
{
    Task<DietEntry?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<int> AddWithItemsAsync(DietEntry meal, IEnumerable<MealItemEntry> items, CancellationToken ct = default);
    Task<List<DietEntry>> ListForUserAsync(Guid userId, DateTime from, DateTime to, bool includeItems = false, CancellationToken ct = default);
    Task UpdateWithItemsAsync(DietEntry updatedMeal, IEnumerable<MealItemEntry> newItems, CancellationToken ct = default);
}

public class DietStore(IDbContextFactory<AppDbContext> factory) : IDietStore
{
    public async Task<DietEntry?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Note: this does NOT include items by default.
        return await db.DietEntries.FindAsync(id);
    }

    public async Task AddAsync(DietEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.DietEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(DietEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.DietEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        // If cascade delete is configured for items, removing the parent is enough.
        // If not, we remove items explicitly (safe either way).
        var meal = await db.DietEntries
            .Include(m => m.Items)
            .SingleOrDefaultAsync(m => m.Id == id);

        if (meal is null) return;

        // Explicit remove for safety (works even without cascade).
        if (meal.Items is { Count: > 0 })
            db.MealItems.RemoveRange(meal.Items);

        db.DietEntries.Remove(meal);
        await db.SaveChangesAsync();
    }

    // -------------------------
    // Diet-specific methods
    // -------------------------

    /// <summary>
    /// Returns a meal including its items.
    /// </summary>
    public async Task<DietEntry?> GetWithItemsAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.DietEntries
            .Include(m => m.Items)
            .SingleOrDefaultAsync(m => m.Id == id, ct);
    }

    /// <summary>
    /// Creates a meal and its items in one call.
    /// </summary>
    public async Task<int> AddWithItemsAsync(DietEntry meal, IEnumerable<MealItemEntry> items, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Ensure navigation + FK are set consistently
        var itemList = items.ToList();
        foreach (var item in itemList)
        {
            item.DietEntry = meal;      // sets relationship
            // item.DietEntryId will be set automatically after SaveChanges if using identity key
        }

        meal.Items = itemList;

        db.DietEntries.Add(meal);
        await db.SaveChangesAsync(ct);

        return meal.Id;
    }

    /// <summary>
    /// Lists meals for a user in a time range. By default, does NOT include items.
    /// </summary>
    public async Task<List<DietEntry>> ListForUserAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        bool includeItems = false,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        IQueryable<DietEntry> query = db.DietEntries;

        if (includeItems)
            query = query.Include(m => m.Items);

        return await query
            .Where(m => m.UserId == userId && m.EatenAt >= from && m.EatenAt <= to)
            .OrderByDescending(m => m.EatenAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Updates the meal scalars and replaces its items wholesale (simple + correct for v1).
    /// </summary>
    public async Task UpdateWithItemsAsync(DietEntry updatedMeal, IEnumerable<MealItemEntry> newItems, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var existing = await db.DietEntries
            .Include(m => m.Items)
            .SingleOrDefaultAsync(m => m.Id == updatedMeal.Id, ct);

        if (existing is null)
            throw new InvalidOperationException($"DietEntry {updatedMeal.Id} not found.");

        // Update scalar fields (adjust for your exact DietEntry fields)
        existing.EatenAt = updatedMeal.EatenAt;
        existing.MealType = updatedMeal.MealType;
        existing.Notes = updatedMeal.Notes;

        // Replace items
        if (existing.Items is { Count: > 0 })
            db.MealItems.RemoveRange(existing.Items);

        var itemList = newItems.ToList();
        foreach (var item in itemList)
        {
            item.Id = 0;                 // ensures EF treats these as new rows (if int identity)
            item.DietEntryId = existing.Id;
        }

        existing.Items = itemList;

        await db.SaveChangesAsync(ct);
    }
}