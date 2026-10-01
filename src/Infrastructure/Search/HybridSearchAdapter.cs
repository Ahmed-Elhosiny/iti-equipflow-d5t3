using EquipFlow.Application.Options;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Services;
using EquipFlow.Domain.Search;
using Microsoft.Extensions.Options;

namespace EquipFlow.Infrastructure.Search;

/// <summary>
/// Hybrid search adapter that combines vector and keyword search
/// using Reciprocal Rank Fusion, implementing ISearchPort facade.
/// </summary>
public sealed class HybridSearchAdapter : ISearchPort
{
     private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorSearchPort _vectorSearchPort;
    private readonly IKeywordSearchPort _keywordSearchPort;
    private readonly ReciprocalRankFusionService _fusionService;
    private readonly IRerankerPort? _rerankerPort;
    private readonly double _relevanceThreshold;

    public HybridSearchAdapter(
        IEmbeddingPort embeddingPort,
        IVectorSearchPort vectorSearchPort,
        IKeywordSearchPort keywordSearchPort,
        ReciprocalRankFusionService fusionService,
        IOptions<AgenticOptions> options,
        IRerankerPort? rerankerPort = null)
    {
        _embeddingPort = embeddingPort;
        _vectorSearchPort = vectorSearchPort;
        _keywordSearchPort = keywordSearchPort;
        _fusionService = fusionService;
        _rerankerPort = rerankerPort;
        _relevanceThreshold = options.Value.MinRelevanceScore;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(
            [query.QueryText],
            cancellationToken);

        if (embeddings.Length != 1)
        {
            throw new InvalidOperationException(
                "The embedding service must return exactly one embedding for a single query.");
        }

         // Execute sequentially to prevent EF Core DbContext thread-safety violations.
        // DbContext is Scoped and not thread-safe; parallel queries on the same context crash.
        var vectorResults = await _vectorSearchPort.SearchAsync(
            query,
            embeddings[0],
            cancellationToken);
            
        var keywordResults = await _keywordSearchPort.SearchAsync(query, cancellationToken);

        IReadOnlyList<RetrievedChunk> candidates = _fusionService.Fuse(
            vectorResults,
            keywordResults);

        if (_rerankerPort is not null)
        {
            candidates = await _rerankerPort.RerankAsync(
                query,
                candidates,
                cancellationToken);
        }

        return candidates
            .Where(candidate => candidate.Score >= _relevanceThreshold)
            .Take(query.TopK)
            .Select((candidate, index) => SearchResult.Create(
                candidate.ChunkId,
                candidate.DocumentId,
                candidate.Content,
                candidate.Score,
                index + 1,
                candidate.Citation))
            .ToList();
    }
}