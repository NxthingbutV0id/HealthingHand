namespace HealthingHand.Web.Services.SleepItems;

public sealed class SleepSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    public int EntryCount { get; set; }
    public double TotalHours { get; set; }
    public double AverageHours { get; set; }
    public double AverageSleepQuality { get; set; }
    public double LongestNightHours { get; set; }
    public double ShortestNightHours { get; set; }
}