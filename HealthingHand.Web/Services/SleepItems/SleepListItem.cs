namespace HealthingHand.Web.Services.SleepItems;

public class SleepListItem
{
    public int Id { get; set; }
    public DateOnly SleepDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public float SleepQuality { get; set; }
    public string Notes { get; set; } = "";
    public double DurationHours { get; set; }
}