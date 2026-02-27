using HealthingHand.Data.Stores;

namespace HealthingHand.Data;

public interface IDatabase
{
    AccountStore Account { get; }
    SleepStore Sleep { get; }
    DietStore Diet { get; }
    WorkoutStore Workout { get; }

    Task InitializeAsync(CancellationToken ct = default);
}