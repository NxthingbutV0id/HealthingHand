namespace HealthingHand.Web.Services.DietItems;

public class DietMealItemInput
{
    public string Name { get; set; } = "";
    public float Quantity { get; set; } = 1;
    public string Unit { get; set; } = "serving";
    public int CaloriesPerUnit { get; set; }
    public float ProteinGrams { get; set; }
    public float CarbsGrams { get; set; }
    public float FatGrams { get; set; }
}