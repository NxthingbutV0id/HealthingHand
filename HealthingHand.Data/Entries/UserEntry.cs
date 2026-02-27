namespace HealthingHand.Data.Entries;

public class UserEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateOnly LastOnline { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public byte Age { get; set; }
    public bool Sex { get; set; }
    public float HeightM { get; set; }
    public float WeightKg { get; set; }
}