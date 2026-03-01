namespace HealthingHand.Data.Entries;

public class WeightEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public float WeightKg { get; set; }
}