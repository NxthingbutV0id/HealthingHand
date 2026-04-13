using HealthingHand.Web.Services.OcrItems;
using Tesseract;

namespace HealthingHand.Web.Services;

public interface IOcrService
{
    Task<(bool Success, string? Error, ScanResult? Result)> ScanAsync(
        Stream imageStream, 
        string? suggestedName = null,
        string? fileName = null,
        string? contentType = null);
}

public sealed class OcrService(IWebHostEnvironment env, IParser parser) : IOcrService
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;
    
    public async Task<(bool Success, string? Error, ScanResult? Result)> ScanAsync(
        Stream imageStream, string? suggestedName = null, string? fileName = null, string? contentType = null)
    {
        if (!imageStream.CanRead)
            return (false, "Image stream is invalid.", null);

        if (!UploadValidator.IsSupportedImage(fileName, contentType)) 
            return (false, "Unsupported image type. Please upload a PNG or JPEG image.", null);
        
        await using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory);

        if (memory.Length < MaxUploadBytes)
            return (false, "Image is too large. Please upload an image smaller than 5 MB.", null);

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

            var rawText = (page.GetText() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
                return (false, "OCR completed, but no text was detected in the image.", null);
            
            var parsed = parser.Parse(rawText, suggestedName);

            var result = new ScanResult
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