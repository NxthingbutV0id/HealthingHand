namespace HealthingHand.Data.Entries;

public enum WeightGoalType
{
    LoseWeight,
    GainWeight,
    MaintainWeight
}

public enum GoalPacePreference
{
    Slow,
    Moderate,
    Aggressive
}

public class WeightGoalEntry
{
    public int Id { get; set; }
    public UserEntry? User { get; set; }
    public Guid UserId { get; set; }
    public float CurrentWeightKg { get; set; }
    public float GoalWeightKg { get; set; }
    public WeightGoalType GoalType { get; set; } = WeightGoalType.MaintainWeight;
    public GoalPacePreference PacePreference { get; set; } = GoalPacePreference.Moderate;
    public string ExerciseFrequency { get; set; } = "Moderate";
    public string ExerciseIntensity { get; set; } = "Medium";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
