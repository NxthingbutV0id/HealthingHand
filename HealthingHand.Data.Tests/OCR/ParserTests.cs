using HealthingHand.Web.Services.OcrItems;

namespace HealthingHand.Data.Tests.OCR;

public class ParserTests
{
    [Fact]
    public void StandardNutritionLabel_ExtractsExpectedFields()
    {
        var parser = new Parser();

        const string text = """
                            Nutrition Facts
                            8 servings per container
                            Serving size 2/3 cup (55g)

                            Calories 230
                            Total Fat 8g
                            Total Carbohydrate 37g
                            Protein 3g
                            """;

        var result = parser.Parse(text, "Granola");

        Assert.Equal("Granola", result.Name);
        Assert.Equal(230, result.Calories);
        Assert.Equal(8f, result.FatGrams);
        Assert.Equal(37f, result.CarbsGrams);
        Assert.Equal(3f, result.ProteinGrams);
        Assert.Equal(8f, result.ServingsPerContainer);
        Assert.Equal(2f / 3f, result.ServingSizeAmount!.Value, 3);
        Assert.Equal("cup", result.ServingSizeUnit);
        Assert.Equal(55f, result.MetricServingAmount);
        Assert.Equal("g", result.MetricServingUnit);
    }

    [Fact]
    public void OcrStyleTextWithDifferentSpacing_StillExtractsFields()
    {
        var parser = new Parser();

        const string text = """
                            NUTRITION FACTS
                            Servings per container: about 2.5
                            Serving size: 1 container (150 g)

                            Calorles 190
                            Total Fat 4g
                            Carbs 28g
                            Proteln 9g
                            """;

        var result = parser.Parse(text, "Yogurt");

        Assert.Equal(190, result.Calories);
        Assert.Equal(4f, result.FatGrams);
        Assert.Equal(28f, result.CarbsGrams);
        Assert.Equal(9f, result.ProteinGrams);
        Assert.Equal(2.5f, result.ServingsPerContainer);
        Assert.Equal(1f, result.ServingSizeAmount);
        Assert.Equal("container", result.ServingSizeUnit);
        Assert.Equal(150f, result.MetricServingAmount);
        Assert.Equal("g", result.MetricServingUnit);
    }

    [Fact]
    public void MissingFields_AddsWarningsInsteadOfThrowing()
    {
        var parser = new Parser();

        const string text = """
                            Nutrition Facts
                            Serving size 1 bar (40g)
                            Calories 180
                            """;

        var result = parser.Parse(text, "Protein Bar");

        Assert.Equal(180, result.Calories);
        Assert.Null(result.ProteinGrams);
        Assert.Null(result.CarbsGrams);
        Assert.Null(result.FatGrams);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("Protein", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("Carbohydrates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("Fat", StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public void ToDietMealItemInput_DefaultsToOneServing()
    {
        var parsed = new ParsedResult
        {
            Name = "Crackers",
            Calories = 160,
            ProteinGrams = 2,
            CarbsGrams = 22,
            FatGrams = 7
        };

        var item = parsed.ToDietMealItemInput();

        Assert.Equal("Crackers", item.Name);
        Assert.Equal(1f, item.Quantity);
        Assert.Equal("serving", item.Unit);
        Assert.Equal(160, item.CaloriesPerUnit);
        Assert.Equal(2f, item.ProteinGrams);
        Assert.Equal(22f, item.CarbsGrams);
        Assert.Equal(7f, item.FatGrams);
    }
}