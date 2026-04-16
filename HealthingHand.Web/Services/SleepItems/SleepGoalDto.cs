namespace HealthingHand.Web.Services.SleepItems;

public sealed class SleepGoalDto
{
    public int Id { get; set; }
    public TimeOnly DesiredWakeTime { get; set; }
    public float PreferredSleepHours { get; set; }
    public TimeOnly BestRecommendedBedtime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<BedtimeRecommendation> Recommendations { get; set; } = [];
}
