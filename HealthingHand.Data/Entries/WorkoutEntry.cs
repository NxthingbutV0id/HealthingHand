namespace HealthingHand.Data.Entries;

public class WorkoutEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public List<ExerciseEntry> Exercises { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string WorkoutType { get; set; } = "";
    public string Notes { get; set; } = "";
}