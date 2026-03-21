namespace HealthingHand.Web.Services.DietItems;

public sealed class DietMealInput
{
    public DateTime EatenAt { get; set; } = DateTime.Now;
    public string MealType { get; set; } = "Meal";
    public string Notes { get; set; } = "";
    public List<DietMealItemInput> Items { get; set; } = [new()];
}