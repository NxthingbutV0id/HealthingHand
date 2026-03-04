namespace HealthingHand.Data.Entries;

public class SleepEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public DateOnly SleepDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public byte SleepQuality { get; set; } // 0-255 scale
    public string Notes { get; set; } = "";
}