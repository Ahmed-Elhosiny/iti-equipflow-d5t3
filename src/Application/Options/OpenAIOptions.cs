namespace EquipFlow.Application.Options;

/// <summary>
/// Configuration options for OpenAI-compatible language model generation.
/// </summary>
/// <param name="Model">The model identifier used for generation.</param>
/// <param name="ApiKey">The API key used to authenticate with the provider.</param>
/// <param name="Endpoint">An optional endpoint for Azure OpenAI or custom proxies.</param>
public sealed record OpenAIOptions(
    string Model,
    string ApiKey,
    string? Endpoint)
{
    public decimal PromptTokenPricePer1K { get; init; } = 0.00015m;

    public decimal CompletionTokenPricePer1K { get; init; } = 0.0006m;

    public Dictionary<string, (decimal Prompt, decimal Completion)> ModelPricing { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o-mini"] = (0.00015m, 0.0006m),
            ["gpt-4o"] = (0.0025m, 0.01m),
            ["gpt-3.5-turbo"] = (0.0005m, 0.0015m)
        };
}
