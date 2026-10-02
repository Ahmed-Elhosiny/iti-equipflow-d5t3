using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EquipFlow.Application.Options;
using EquipFlow.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EquipFlow.Infrastructure.AI;

public sealed class OllamaEmbeddingAdapter(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    ILogger<OllamaEmbeddingAdapter> logger) : IEmbeddingPort
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<ReadOnlyMemory<float>[]> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts is null || texts.Count == 0)
        {
            return [];
        }

        var requestPayload = new { model = _options.EmbeddingModel, input = texts.ToArray() };
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/embed")
        {
            Content = JsonContent.Create(requestPayload)
        };

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);

            if (result?.Embeddings is null || result.Embeddings.Length != texts.Count)
            {
                throw new InvalidOperationException($"Ollama embedding response mismatch. Expected {texts.Count}, got {result?.Embeddings?.Length ?? 0}.");
            }

            return result.Embeddings.Select(e => new ReadOnlyMemory<float>(e)).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate embeddings using Ollama model {Model}.", _options.EmbeddingModel);
            throw;
        }
    }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; init; }
    }
}
