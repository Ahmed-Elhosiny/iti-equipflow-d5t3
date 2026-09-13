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
    string? Endpoint);
