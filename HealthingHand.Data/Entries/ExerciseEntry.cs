namespace HealthingHand.Data.Entries;

public class ExerciseEntry
{
    public int Id { get; set; }
    public WorkoutEntry? WorkoutEntry { get; set; }
    public int WorkoutId { get; set; }
    public string Name { get; set; } = "";
    public int Sets { get; set; }
    public int Reps { get; set; }
    public float WeightKg { get; set; }
    public float DistanceKm { get; set; }
    public TimeSpan Time { get; set; }
}