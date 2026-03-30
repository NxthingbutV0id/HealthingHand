namespace HealthingHand.Data.Entries;

public class MealItemEntry
{
    public int Id { get; set; }
    public DietEntry? DietEntry { get; set; }
    public int DietEntryId { get; set; }
    public string Name { get; set; } = "";
    public float Quantity { get; set; }
    public string Unit { get; set; } = "";
    public int TotalCalories { get; set; }
    public float ProteinGrams { get; set; }
    public float CarbsGrams { get; set; }
    public float FatGrams { get; set; }
}