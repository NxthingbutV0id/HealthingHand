namespace HealthingHand.Web.Services.DietItems;

public class DietMealListItem
{
    public int Id { get; set; }
    public DateTime EatenAt { get; set; }
    public string MealType { get; set; } = "";
    public string Notes { get; set; } = "";
    public int ItemCount { get; set; }
    public int TotalCalories { get; set; }
    public float TotalProteinGrams { get; set; }
    public float TotalCarbsGrams { get; set; }
    public float TotalFatGrams { get; set; }
}