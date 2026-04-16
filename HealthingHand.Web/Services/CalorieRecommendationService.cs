using HealthingHand.Data.Entries;
using HealthingHand.Web.Services.WeightItems;

namespace HealthingHand.Web.Services;

public interface ICalorieRecommendationService
{
    CalorieRecommendationDto Calculate(CalorieRecommendationInput input);
}

public class CalorieRecommendationService : ICalorieRecommendationService
{
    private const int MinimumDailyCalories = 1200;

    public CalorieRecommendationDto Calculate(CalorieRecommendationInput input)
    {
        var maintenanceCalories = EstimateMaintenanceCalories(input.CurrentWeightKg, input.ExerciseFrequency, input.ExerciseIntensity);
        var adjustment = GetGoalAdjustment(input.GoalType, input.PacePreference);
        var rawRecommendation = maintenanceCalories + adjustment;
        var recommendedCalories = Math.Max(MinimumDailyCalories, rawRecommendation);

        return new CalorieRecommendationDto
        {
            EstimatedMaintenanceCalories = maintenanceCalories,
            RecommendedDailyCalories = recommendedCalories,
            UsedMinimumFloor = recommendedCalories != rawRecommendation,
            Warning = recommendedCalories != rawRecommendation
                ? "This recommendation hit the 1200 calorie minimum floor. Consider a slower pace or professional guidance."
                : null,
            // This is a general wellness estimate based on body weight and activity, not medical advice.
            ReasoningSummary = BuildSummary(input, maintenanceCalories, adjustment, recommendedCalories != rawRecommendation)
        };
    }

    private static int EstimateMaintenanceCalories(
        float currentWeightKg,
        ExerciseFrequency exerciseFrequency,
        ExerciseIntensity exerciseIntensity)
    {
        var currentWeightLb = currentWeightKg * 2.20462f;
        var multiplier = exerciseFrequency switch
        {
            ExerciseFrequency.Sedentary => 12.0,
            ExerciseFrequency.Light => 13.0,
            ExerciseFrequency.Moderate => 15.0,
            ExerciseFrequency.Active => 16.5,
            _ => 15.0
        };

        multiplier += exerciseIntensity switch
        {
            ExerciseIntensity.Low => -0.25,
            ExerciseIntensity.Medium => 0.0,
            ExerciseIntensity.High => 0.5,
            _ => 0.0
        };

        return (int)Math.Round(currentWeightLb * multiplier);
    }

    private static int GetGoalAdjustment(WeightGoalType goalType, GoalPacePreference pacePreference)
    {
        return goalType switch
        {
            WeightGoalType.LoseWeight => pacePreference switch
            {
                GoalPacePreference.Slow => -250,
                GoalPacePreference.Moderate => -500,
                GoalPacePreference.Aggressive => -750,
                _ => -500
            },
            WeightGoalType.GainWeight => pacePreference switch
            {
                GoalPacePreference.Slow => 200,
                GoalPacePreference.Moderate => 350,
                GoalPacePreference.Aggressive => 500,
                _ => 350
            },
            _ => 0
        };
    }

    private static string BuildSummary(
        CalorieRecommendationInput input,
        int maintenanceCalories,
        int adjustment,
        bool usedMinimumFloor)
    {
        var activityLabel = $"{input.ExerciseFrequency.ToString().ToLowerInvariant()} activity with {input.ExerciseIntensity.ToString().ToLowerInvariant()} intensity";

        if (input.GoalType == WeightGoalType.MaintainWeight)
            return $"Estimated maintenance is about {maintenanceCalories} calories per day based on {activityLabel}, so the recommendation stays at maintenance.";

        var direction = adjustment < 0 ? "deficit" : "surplus";
        var amount = Math.Abs(adjustment);
        var floorNote = usedMinimumFloor ? " The recommendation was raised to the minimum safety floor." : "";

        return $"Estimated maintenance is about {maintenanceCalories} calories per day based on {activityLabel}. A {amount} calorie {direction} was applied for a {input.PacePreference.ToString().ToLowerInvariant()} {input.GoalType.ToString().Replace("Weight", "").ToLowerInvariant()} goal.{floorNote}";
    }
}
