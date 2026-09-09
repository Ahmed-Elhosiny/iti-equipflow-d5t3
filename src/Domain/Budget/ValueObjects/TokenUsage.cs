namespace EquipFlow.Domain.Budget.ValueObjects;

public readonly record struct TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;

    public Money CalculateCost(decimal pricePerThousandTokens) =>
        Money.FromDecimal(TotalTokens / 1000m * pricePerThousandTokens);
}