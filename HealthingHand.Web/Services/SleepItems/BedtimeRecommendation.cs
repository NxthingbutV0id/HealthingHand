namespace HealthingHand.Web.Services.SleepItems;

public sealed class BedtimeRecommendation
{
    public int Cycles { get; set; }
    public float SleepHours { get; set; }
    public TimeOnly Bedtime { get; set; }
    public bool IsBest { get; set; }
    public bool IsClosestToGoal { get; set; }
}
