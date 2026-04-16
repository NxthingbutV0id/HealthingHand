using HealthingHand.Data.Entries;

namespace HealthingHand.Web.Services.WeightItems;

public sealed class CalorieRecommendationInput
{
    public float CurrentWeightKg { get; set; }
    public float GoalWeightKg { get; set; }
    public WeightGoalType GoalType { get; set; } = WeightGoalType.MaintainWeight;
    public GoalPacePreference PacePreference { get; set; } = GoalPacePreference.Moderate;
    public ExerciseFrequency ExerciseFrequency { get; set; } = ExerciseFrequency.Moderate;
    public ExerciseIntensity ExerciseIntensity { get; set; } = ExerciseIntensity.Medium;
}
