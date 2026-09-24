using System.Collections.Concurrent;
using EquipFlow.Application.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Search;

public sealed class InMemorySemanticCacheAdapter(
    IEmbeddingPort embeddingPort,
    ILogger<InMemorySemanticCacheAdapter> logger) : ICachePort
{
    private readonly ConcurrentDictionary<string, (ReadOnlyMemory<float> Embedding, string Response)> _cache = new();
    private const double SimilarityThreshold = 0.85;

    public async Task<SemanticCacheMatch?> FindSemanticMatchAsync(
        string? semanticQuery,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semanticQuery))
        {
            return null;
        }

        var embeddings = await embeddingPort.GenerateEmbeddingsAsync(
            new List<string> { semanticQuery }, 
            cancellationToken);
            
        var queryEmbedding = embeddings[0];
        
        foreach (var entry in _cache.Values)
        {
            var similarity = CosineSimilarity(queryEmbedding.Span, entry.Embedding.Span);
            if (similarity >= SimilarityThreshold)
            {
                return new SemanticCacheMatch(semanticQuery, entry.Response);
            }
        }

        return null;
    }

    public async Task AddAsync(string query, string response, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        try
        {
            var embeddings = await embeddingPort.GenerateEmbeddingsAsync(
                new List<string> { query }, 
                cancellationToken);
                
            var queryEmbedding = embeddings[0];
            _cache[query] = (queryEmbedding, response);
        }
        catch (Exception ex)
        {
            // Fail-open for cache writes: do not let cache infrastructure failures break the main workflow
            logger.LogWarning(ex, "Failed to generate embedding for semantic cache write.");
        }
    }

    private static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}