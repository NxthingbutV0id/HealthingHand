using HealthingHand.Web.Services.OcrItems;
using Tesseract;

namespace HealthingHand.Web.Services;

public interface INutritionLabelOcrService
{
    Task<(bool Success, string? Error, NutritionLabelScanResult? Result)> ScanAsync(
        Stream imageStream, string? suggestedName = null);
}

public sealed class NutritionLabelOcrService(IWebHostEnvironment env, INutritionLabelParser parser) : INutritionLabelOcrService
{
    public async Task<(bool Success, string? Error, NutritionLabelScanResult? Result)> ScanAsync(
        Stream imageStream, string? suggestedName = null)
    {
        if (!imageStream.CanRead)
            return (false, "Image stream is invalid.", null);

        await using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory);

        if (memory.Length == 0)
            return (false, "Uploaded image was empty.", null);

        var tessdataPath = Path.Combine(env.ContentRootPath, "tessdata");
        if (!Directory.Exists(tessdataPath))
            return (false, $"tessdata folder not found at '{tessdataPath}'.", null);

        try
        {
            using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
            using var image = Pix.LoadFromMemory(memory.ToArray());
            using var page = engine.Process(image, PageSegMode.Auto);

            var rawText = page.GetText() ?? string.Empty;
            var parsed = parser.Parse(rawText, suggestedName);

            var result = new NutritionLabelScanResult
            {
                RawText = rawText,
                Confidence = page.GetMeanConfidence(),
                Parsed = parsed
            };

            return (true, null, result);
        }
        catch (Exception ex) when (ex is TesseractException or InvalidOperationException or IOException)
        {
            return (false, $"OCR failed: {ex.Message}", null);
        }
    }
}