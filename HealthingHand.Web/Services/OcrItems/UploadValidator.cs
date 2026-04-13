namespace HealthingHand.Web.Services.OcrItems;

public class UploadValidator
{
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    public static bool IsSupportedImage(string? fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            SupportedContentTypes.Contains(contentType))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        return !string.IsNullOrWhiteSpace(extension) &&
               SupportedExtensions.Contains(extension);
    }
}