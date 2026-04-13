namespace HealthingHand.Web.Services.OcrItems;

public sealed class ScanResult
{
    public string RawText { get; set; } = "";
    public float Confidence { get; set; }
    public ParsedResult Parsed { get; set; } = new();
}