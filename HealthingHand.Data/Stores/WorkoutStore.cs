using HealthingHand.Data.Entries;

namespace HealthingHand.Data.Stores;

public interface IWorkoutStore : IStore<WorkoutEntry, int>
{
    //TODO
}

public class WorkoutStore : IWorkoutStore
{
    public Task<WorkoutEntry?> GetAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(WorkoutEntry entry)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(WorkoutEntry entry)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(int id)
    {
        throw new NotImplementedException();
    }
}