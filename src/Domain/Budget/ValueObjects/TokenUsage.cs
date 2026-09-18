namespace EquipFlow.Domain.Budget.ValueObjects;

public readonly record struct TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;

    public static TokenUsage FromActual(int prompt, int completion) =>
        new(prompt, completion);

    public static TokenUsage FromEstimated(int estimated) =>
        new(estimated, 0);

    public Money CalculateCost(decimal pricePerThousandTokens) =>
        Money.FromDecimal(TotalTokens / 1000m * pricePerThousandTokens);
}