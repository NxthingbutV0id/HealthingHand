namespace HealthingHand.Web.Services.OcrItems;

public sealed class NutritionLabelScanResult
{
    public string RawText { get; set; } = "";
    public float Confidence { get; set; }
    public NutritionLabelParsedResult Parsed { get; set; } = new();
}