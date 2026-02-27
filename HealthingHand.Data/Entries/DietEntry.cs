namespace HealthingHand.Data.Entries;

public class DietEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public DateTime EatenAt { get; set; }
    public List<MealItemEntry> Items { get; set; } = [];
    public string MealType { get; set; } = "";
    public string Notes { get; set; } = "";
}