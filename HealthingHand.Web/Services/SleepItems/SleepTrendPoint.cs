namespace HealthingHand.Web.Services.SleepItems;

public sealed class SleepTrendPoint
{
    public DateOnly SleepDate { get; set; }
    public double DurationHours { get; set; }
    public float SleepQuality { get; set; }
}