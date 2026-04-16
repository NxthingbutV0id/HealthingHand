namespace HealthingHand.Web.Services.WeightItems;

public sealed class CalorieRecommendationDto
{
    public int EstimatedMaintenanceCalories { get; set; }
    public int RecommendedDailyCalories { get; set; }
    public string ReasoningSummary { get; set; } = "";
    public string? Warning { get; set; }
    public bool UsedMinimumFloor { get; set; }
}
