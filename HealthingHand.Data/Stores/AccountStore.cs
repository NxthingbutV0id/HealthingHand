using HealthingHand.Data.Entries;

namespace HealthingHand.Data.Stores;

public interface IAccountStore : IStore<UserEntry, Guid>
{
    
}

public class AccountStore : IAccountStore
{
    public Task<UserEntry?> GetAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(UserEntry entry)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(UserEntry entry)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}