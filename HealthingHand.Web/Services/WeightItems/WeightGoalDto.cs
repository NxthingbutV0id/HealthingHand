using HealthingHand.Data.Entries;

namespace HealthingHand.Web.Services.WeightItems;

public sealed class WeightGoalDto
{
    public int Id { get; set; }
    public float CurrentWeightKg { get; set; }
    public float GoalWeightKg { get; set; }
    public float DifferenceKg { get; set; }
    public WeightGoalType GoalType { get; set; } = WeightGoalType.MaintainWeight;
    public GoalPacePreference PacePreference { get; set; } = GoalPacePreference.Moderate;
    public ExerciseFrequency ExerciseFrequency { get; set; } = ExerciseFrequency.Moderate;
    public ExerciseIntensity ExerciseIntensity { get; set; } = ExerciseIntensity.Medium;
    public string RecommendedDirection { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
