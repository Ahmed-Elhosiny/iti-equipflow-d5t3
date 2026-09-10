using EquipFlow.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace EquipFlow.Infrastructure.AI;

public sealed class OpenAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "text-embedding-3-small";
}

public sealed class OpenAiEmbeddingAdapter : IEmbeddingPort
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<OpenAiEmbeddingAdapter> _logger;

    public OpenAiEmbeddingAdapter(
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiEmbeddingAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new ArgumentException("OpenAI API key is required.", nameof(options));
        }

        _client = new EmbeddingClient(options.Value.Model, options.Value.ApiKey);
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<float>[]> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        try
        {
            var response = await _client.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);
            return response.Value.Select(embedding => embedding.ToFloats()).ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "OpenAI embedding generation failed for {TextCount} texts.", texts.Count);
            throw new EmbeddingGenerationException("Unable to generate embeddings with OpenAI.", exception);
        }
    }
}

public sealed class EmbeddingGenerationException : Exception
{
    public EmbeddingGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}