using HealthingHand.Data.Entries;

namespace HealthingHand.Web.Services.WorkoutItems;

public static class WorkoutActivityMetCatalog
{
    public static IReadOnlyList<ExerciseActivityType> All { get; } =
    [
        ExerciseActivityType.GeneralWeightLifting,
        ExerciseActivityType.Running,
        ExerciseActivityType.Walking,
        ExerciseActivityType.Cycling,
        ExerciseActivityType.BodyweightCalisthenics,
        ExerciseActivityType.Rowing
    ];

    public static double GetMet(ExerciseActivityType activityType) => activityType switch
    {
        ExerciseActivityType.GeneralWeightLifting => 3.5,
        ExerciseActivityType.Walking => 3.5,
        ExerciseActivityType.BodyweightCalisthenics => 4.0,
        ExerciseActivityType.Rowing => 7.0,
        ExerciseActivityType.Cycling => 7.5,
        ExerciseActivityType.Running => 9.8,
        _ => 0
    };

    public static string ToDisplayName(ExerciseActivityType activityType) => activityType switch
    {
        ExerciseActivityType.GeneralWeightLifting => "General weight lifting",
        ExerciseActivityType.BodyweightCalisthenics => "Bodyweight / calisthenics",
        _ => activityType.ToString()
    };
}
