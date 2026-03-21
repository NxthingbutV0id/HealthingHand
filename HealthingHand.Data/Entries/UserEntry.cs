namespace HealthingHand.Data.Entries;

public enum Sex
{
    Undefined, Male, Female
}

public class UserEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime LastOnline { get; set; } = DateTime.UtcNow;
    public byte Age { get; set; }
    public Sex Sex { get; set; } = Sex.Undefined;
    public float HeightM { get; set; }
}