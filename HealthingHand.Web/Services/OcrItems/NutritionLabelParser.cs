using System.Globalization;
using System.Text.RegularExpressions;

namespace HealthingHand.Web.Services.OcrItems;

public interface INutritionLabelParser
{
    NutritionLabelParsedResult Parse(string rawText, string? suggestedName = null);
}

public sealed partial class NutritionLabelParser : INutritionLabelParser
{
    public NutritionLabelParsedResult Parse(string rawText, string? suggestedName = null)
    {
        var normalizedText = Normalize(rawText);

        var result = new NutritionLabelParsedResult
        {
            Name = string.IsNullOrWhiteSpace(suggestedName) ? "Scanned item" : suggestedName.Trim(),
            RawText = rawText,
            NormalizedText = normalizedText,
            Calories = ExtractInt(CaloriesRegex(), normalizedText, "value"),
            ProteinGrams = ExtractFloat(ProteinRegex(), normalizedText, "value"),
            CarbsGrams = ExtractFloatEither(CarbsRegex(), normalizedText, "value", "value2"),
            FatGrams = ExtractFloat(FatRegex(), normalizedText, "value"),
            ServingsPerContainer = ExtractFloat(ServingsPerContainerRegex(), normalizedText, "value")
        };

        var servingSizeText = ExtractString(ServingSizeRegex(), normalizedText, "value");
        result.ServingSizeText = servingSizeText ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(servingSizeText))
            ParseServingSize(servingSizeText, result);

        AddWarnings(result);

        return result;
    }

    private static string Normalize(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        var text = rawText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace('’', '\'')
            .Replace('‘', '\'')
            .Replace('“', '"')
            .Replace('”', '"')
            .Replace('—', '-')
            .Replace('–', '-');

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeLine)
            .Where(l => !string.IsNullOrWhiteSpace(l));

        return string.Join('\n', lines);
    }

    private static string NormalizeLine(string line)
    {
        var normalized = line.ToLowerInvariant();

        // Clean out common errors during scan (i -> l)
        normalized = normalized
            .Replace("calorles", "calories")
            .Replace("proteln", "protein")
            .Replace("protem", "protein")
            .Replace("carbohydrale", "carbohydrate")
            .Replace("servlng", "serving")
            .Replace("contalner", "container");

        normalized = NormalizedRegex().Replace(normalized, " ").Trim();

        return normalized;
    }

    private static void ParseServingSize(string servingSizeText, NutritionLabelParsedResult result)
    {
        var leadingMatch = LeadingAmountUnitRegex().Match(servingSizeText);
        if (leadingMatch.Success)
        {
            result.ServingSizeAmount = ParseFlexibleNumber(leadingMatch.Groups["amount"].Value);
            result.ServingSizeUnit = leadingMatch.Groups["unit"].Value.Trim();
        }

        var metricMatch = ParentheticalMetricRegex().Match(servingSizeText);
        if (metricMatch.Success)
        {
            result.MetricServingAmount = ParseFlexibleNumber(metricMatch.Groups["amount"].Value);
            result.MetricServingUnit = metricMatch.Groups["unit"].Value.Trim().ToLowerInvariant();
        }

        if (result.ServingSizeAmount is not null || result.MetricServingAmount is null) return;
        result.ServingSizeAmount = result.MetricServingAmount;
        result.ServingSizeUnit = result.MetricServingUnit;
    }

    private static void AddWarnings(NutritionLabelParsedResult result)
    {
        if (result.Calories is null) result.Warnings.Add("Calories could not be detected.");
        if (result.ProteinGrams is null) result.Warnings.Add("Protein could not be detected.");
        if (result.CarbsGrams is null) result.Warnings.Add("Carbohydrates could not be detected.");
        if (result.FatGrams is null) result.Warnings.Add("Fat could not be detected.");
        if (string.IsNullOrWhiteSpace(result.ServingSizeText)) result.Warnings.Add("Serving size could not be detected.");
    }

    private static int? ExtractInt(Regex regex, string text, string groupName)
    {
        var match = regex.Match(text);
        if (!match.Success) return null;

        return int.TryParse(match.Groups[groupName].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static float? ExtractFloat(Regex regex, string text, string groupName)
    {
        var match = regex.Match(text);
        if (!match.Success) return null;

        return float.TryParse(match.Groups[groupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static float? ExtractFloatEither(Regex regex, string text, string primaryGroup, string fallbackGroup)
    {
        var match = regex.Match(text);
        if (!match.Success) return null;

        var raw = match.Groups[primaryGroup].Success
            ? match.Groups[primaryGroup].Value
            : match.Groups[fallbackGroup].Value;

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? ExtractString(Regex regex, string text, string groupName)
    {
        var match = regex.Match(text);
        if (!match.Success) return null;

        var value = match.Groups[groupName].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static float? ParseFlexibleNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        raw = raw.Trim();

        if (raw.Contains(' '))
        {
            var pieces = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 2)
            {
                var whole = ParseFlexibleNumber(pieces[0]);
                var fraction = ParseFlexibleNumber(pieces[1]);

                if (whole is not null && fraction is not null)
                    return whole.Value + fraction.Value;
            }
        }

        if (raw.Contains('/'))
        {
            var pieces = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 2 &&
                float.TryParse(pieces[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
                float.TryParse(pieces[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    // Regex hell...
    [GeneratedRegex(@"(?im)\bcalories\b\s*[:\-]?\s*(?<value>\d{1,4})\b", RegexOptions.Compiled, "en-US")]
    private static partial Regex CaloriesRegex();
    [GeneratedRegex(@"(?im)\bprotein\b\s*[:\-]?\s*(?<value>\d+(?:\.\d+)?)\s*g\b", RegexOptions.Compiled, "en-US")]
    private static partial Regex ProteinRegex();
    [GeneratedRegex(@"(?im)\b(?:total\s+)?carbohydrates?\b\s*[:\-]?\s*(?<value>\d+(?:\.\d+)?)\s*g\b|\b(?:total\s+)?carbs?\b\s*[:\-]?\s*(?<value2>\d+(?:\.\d+)?)\s*g\b", RegexOptions.Compiled, "en-US")]
    private static partial Regex CarbsRegex();
    [GeneratedRegex(@"(?im)\b(?:total\s+)?fat\b\s*[:\-]?\s*(?<value>\d+(?:\.\d+)?)\s*g\b", RegexOptions.Compiled, "en-US")]
    private static partial Regex FatRegex();
    [GeneratedRegex(@"(?im)\bservings?\s+per\s+container\b\s*[:\-]?\s*(?:about\s+)?(?<value>\d+(?:\.\d+)?)\b", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServingsPerContainerRegex();
    [GeneratedRegex(@"(?im)^\s*serving\s+size\b\s*[:\-]?\s*(?<value>.+?)\s*$", RegexOptions.Compiled, "en-US")]
    private static partial Regex ServingSizeRegex();
    [GeneratedRegex(@"^\s*(?<amount>\d+\s+\d+/\d+|\d+/\d+|\d+(?:\.\d+)?)\s*(?<unit>[a-zA-Z]+(?:\s+[a-zA-Z]+)?)?", RegexOptions.Compiled)]
    private static partial Regex LeadingAmountUnitRegex();
    [GeneratedRegex(@"\((?<amount>\d+(?:\.\d+)?)\s*(?<unit>g|gram|grams|ml|l|oz)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex ParentheticalMetricRegex();
    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex NormalizedRegex();
}