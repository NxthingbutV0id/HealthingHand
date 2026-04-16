namespace HealthingHand.Data.Entries;

public class SleepGoalEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public TimeOnly DesiredWakeTime { get; set; }
    public float PreferredSleepHours { get; set; }
    public TimeOnly BestRecommendedBedtime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
