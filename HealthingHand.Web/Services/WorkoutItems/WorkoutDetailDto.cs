using HealthingHand.Data.Entries;

namespace HealthingHand.Web.Services.WorkoutItems;

public sealed class WorkoutDetailDto
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public WorkoutType WorkoutType { get; set; } = WorkoutType.Undefined;
    public int SelfReportedIntensity { get; set; }
    public int AverageHeartRate { get; set; }
    public float? UserWeightKg { get; set; }
    public double? EstimatedCaloriesBurned { get; set; }
    public string Notes { get; set; } = "";
    public List<WorkoutExerciseItem> Exercises { get; set; } = [];
}
