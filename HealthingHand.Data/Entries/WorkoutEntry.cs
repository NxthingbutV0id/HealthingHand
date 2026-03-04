namespace HealthingHand.Data.Entries;

public enum WorkoutType
{
    Undefined,
    Cardio,
    StrengthTraining,
    Flexibility,
    Balance,
    HighIntensityIntervalTraining,
    Yoga,
    Pilates,
    CrossFit,
    Other
}

public class WorkoutEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public List<ExerciseEntry> Exercises { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public WorkoutType WorkoutType { get; set; } = WorkoutType.Undefined;
    public string Notes { get; set; } = "";
}