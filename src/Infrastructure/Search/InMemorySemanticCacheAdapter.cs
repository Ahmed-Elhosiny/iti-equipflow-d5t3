using System.Collections.Concurrent;
using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Search;

public sealed class InMemorySemanticCacheAdapter : ICachePort
{
    private readonly IEmbeddingPort _embeddingPort;
    private readonly ConcurrentDictionary<string, (ReadOnlyMemory<float> Embedding, string Response)> _cache = new();
    private const double SimilarityThreshold = 0.85;

    public InMemorySemanticCacheAdapter(IEmbeddingPort embeddingPort)
    {
        _embeddingPort = embeddingPort;
    }

    public async Task<SemanticCacheMatch?> FindSemanticMatchAsync(
        string? semanticQuery,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semanticQuery))
        {
            return null;
        }

        var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(
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

    // Helper method to populate the cache (can be called by the orchestrator after successful runs)
    public void AddToCache(string query, string response, ReadOnlyMemory<float> embedding)
    {
        _cache[query] = (embedding, response);
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