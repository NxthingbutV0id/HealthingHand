namespace HealthingHand.Web.Services.WeightItems;

public sealed class WeightEntryInput
{
    public DateTime Date { get; set; } = DateTime.Today;
    public float WeightKg { get; set; }
}
