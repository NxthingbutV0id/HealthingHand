namespace HealthingHand.Data.Entries;

public class SleepEntry
{
    public int Id { get; set; }
    public User? User { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    // TODO: add data as needed
}