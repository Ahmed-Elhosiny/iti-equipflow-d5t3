namespace EquipFlow.Application.Options;

/// <summary>
/// Configuration options for OpenAI-compatible language model generation.
/// </summary>
public sealed class OpenAIOptions
{
    public string Model { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = string.Empty;
    public string? Endpoint { get; set; }

    public decimal PromptTokenPricePer1K { get; set; } = 0.00015m;

    public decimal CompletionTokenPricePer1K { get; set; } = 0.0006m;

    public Dictionary<string, (decimal Prompt, decimal Completion)> ModelPricing { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o-mini"] = (0.00015m, 0.0006m),
            ["gpt-4o"] = (0.0025m, 0.01m),
            ["gpt-3.5-turbo"] = (0.0005m, 0.0015m)
        };

    // Required for IOptions<T> configuration binding
    public OpenAIOptions() { }

    // Used by CostGovernorService fallback instantiation
    public OpenAIOptions(string model, string apiKey, string? endpoint)
    {
        Model = model;
        ApiKey = apiKey;
        Endpoint = endpoint;
    }
}