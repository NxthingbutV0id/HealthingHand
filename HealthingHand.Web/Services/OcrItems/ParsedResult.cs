using HealthingHand.Web.Services.DietItems;

namespace HealthingHand.Web.Services.OcrItems;

public sealed class ParsedResult
{
    public string Name { get; set; } = "Scanned item";
    public string RawText { get; set; } = "";
    public string NormalizedText { get; set; } = "";

    public int? Calories { get; set; }
    public float? ProteinGrams { get; set; }
    public float? CarbsGrams { get; set; }
    public float? FatGrams { get; set; }

    public float? ServingsPerContainer { get; set; }

    public string ServingSizeText { get; set; } = "";
    public float? ServingSizeAmount { get; set; }
    public string ServingSizeUnit { get; set; } = "";
    public float? MetricServingAmount { get; set; }
    public string MetricServingUnit { get; set; } = "";

    public List<string> Warnings { get; set; } = [];

    public DietMealItemInput ToDietMealItemInput()
    {
        return new DietMealItemInput
        {
            Name = Name,
            Quantity = 1,
            Unit = "serving",
            CaloriesPerUnit = Calories ?? 0,
            ProteinGrams = ProteinGrams ?? 0,
            CarbsGrams = CarbsGrams ?? 0,
            FatGrams = FatGrams ?? 0
        };
    }
}