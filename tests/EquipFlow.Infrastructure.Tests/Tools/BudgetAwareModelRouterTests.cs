using EquipFlow.Application.Options;
using EquipFlow.Infrastructure.LLM;
using Microsoft.Extensions.Options;
using Xunit;

namespace EquipFlow.Infrastructure.Tests.Tools;

public class BudgetAwareModelRouterTests
{
    private readonly BudgetAwareModelRouter _router;

    public BudgetAwareModelRouterTests()
    {
        // Use default OpenAIOptions which includes the ModelPricing dictionary
        var options = Options.Create(new OpenAIOptions("gpt-4o", "fake-key", null));
        _router = new BudgetAwareModelRouter(options);
    }

    [Fact]
    public async Task GetCheaperModelAsync_ReturnsGpt4oMini_WhenCurrentPriceIsHigh()
    {
        // Arrange
        decimal highPrice = 0.01m; // e.g., gpt-4o price

        // Act
        var result = await _router.GetCheaperModelAsync(highPrice);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("gpt-4o-mini", result!.ModelName);
        Assert.True(result.PricePerThousandTokens < highPrice);
    }

    [Fact]
    public async Task GetCheaperModelAsync_ReturnsOllama_WhenCurrentPriceIsGpt4oMini()
    {
        // Arrange
        decimal miniPrice = 0.00015m;

        // Act
        var result = await _router.GetCheaperModelAsync(miniPrice);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Ollama", result!.ModelName);
        Assert.Equal(0m, result.PricePerThousandTokens);
    }

    [Fact]
    public async Task GetCheaperModelAsync_ReturnsNull_WhenCurrentPriceIsZero()
    {
        // Arrange
        decimal zeroPrice = 0m;

        // Act
        var result = await _router.GetCheaperModelAsync(zeroPrice);

        // Assert
        Assert.Null(result);
    }
}