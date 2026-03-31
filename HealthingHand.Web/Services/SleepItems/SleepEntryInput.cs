namespace HealthingHand.Web.Services.SleepItems;

public class SleepEntryInput
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public float SleepQuality { get; set; }
    public string Notes { get; set; } = "";
}