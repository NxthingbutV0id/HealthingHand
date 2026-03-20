namespace HealthingHand.Web.Services.DietItems;

public class DietSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int MealCount { get; set; }
    public int ItemCount { get; set; }
    public int TotalCalories { get; set; }
    public float TotalProteinGrams { get; set; }
    public float TotalCarbsGrams { get; set; }
    public float TotalFatGrams { get; set; }
    public double AverageCaloriesPerMeal { get; set; }
}