using HealthingHand.Data.Stores;

namespace HealthingHand.Data;

public interface IDatabase
{
    IAccountStore Account { get; }
    ISleepStore Sleep { get; }
    IDietStore Diet { get; }
    IWorkoutStore Workout { get; }

    Task InitializeAsync(CancellationToken ct = default);
}