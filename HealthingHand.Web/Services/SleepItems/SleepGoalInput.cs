namespace HealthingHand.Web.Services.SleepItems;

public sealed class SleepGoalInput
{
    public TimeOnly DesiredWakeTime { get; set; } = new(7, 0);
    public float PreferredSleepHours { get; set; } = 7.5f;
}
