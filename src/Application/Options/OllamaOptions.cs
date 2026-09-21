namespace EquipFlow.Application.Options;

/// <summary>
/// Configuration options for Ollama language model generation.
/// </summary>
/// <param name="BaseUrl">The base URL of the Ollama server.</param>
/// <param name="Model">The model identifier used for generation.</param>
/// <param name="EmbeddingModel">The model identifier used for embeddings.</param>
public sealed class OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "qwen2.5:7b-instruct";
    public string EmbeddingModel { get; init; } = "mxbai-embed-large";
}