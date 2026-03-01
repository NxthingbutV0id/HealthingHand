using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Stores;

public interface IAccountStore : IStore<UserEntry, Guid>
{
    Task<UserEntry?> GetByEmailAsync(string email);
}

public class AccountStore(IDbContextFactory<AppDbContext> factory) : IAccountStore
{
    public async Task<UserEntry?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.FindAsync(id);
    }

    public async Task AddAsync(UserEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Users.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserEntry entry)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Users.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.Users.FindAsync(id);
        if (entry is null) return;
        db.Users.Remove(entry);
        await db.SaveChangesAsync();
    }

    public async Task<UserEntry?> GetByEmailAsync(string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);
    }
}