using HealthingHand.Data.Entries;

namespace HealthingHand.Web.Services.WorkoutItems;

public sealed class WorkoutExerciseInput
{
    public string Name { get; set; } = "";
    public ExerciseActivityType ActivityType { get; set; } = ExerciseActivityType.GeneralWeightLifting;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public float WeightKg { get; set; }
    public float DistanceKm { get; set; }
    public int DurationMinutes { get; set; }
}
