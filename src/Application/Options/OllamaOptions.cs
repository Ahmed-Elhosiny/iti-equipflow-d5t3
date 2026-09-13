namespace EquipFlow.Application.Options;

/// <summary>
/// Configuration options for Ollama language model generation.
/// </summary>
/// <param name="BaseUrl">The base URL of the Ollama server.</param>
/// <param name="Model">The model identifier used for generation.</param>
public sealed record OllamaOptions(
    string BaseUrl,
    string Model);