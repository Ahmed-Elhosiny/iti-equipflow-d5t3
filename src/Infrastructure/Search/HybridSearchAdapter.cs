using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Services;
using EquipFlow.Domain.Search;

namespace EquipFlow.Infrastructure.Search;

/// <summary>
/// Hybrid search adapter that combines vector and keyword search
/// using Reciprocal Rank Fusion, implementing ISearchPort facade.
/// </summary>
public sealed class HybridSearchAdapter : ISearchPort
{
    private const double RelevanceThreshold = 0.0;

    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorSearchPort _vectorSearchPort;
    private readonly IKeywordSearchPort _keywordSearchPort;
    private readonly ReciprocalRankFusionService _fusionService;
    private readonly IRerankerPort? _rerankerPort;

    public HybridSearchAdapter(
        IEmbeddingPort embeddingPort,
        IVectorSearchPort vectorSearchPort,
        IKeywordSearchPort keywordSearchPort,
        ReciprocalRankFusionService fusionService,
        IRerankerPort? rerankerPort = null)
    {
        _embeddingPort = embeddingPort;
        _vectorSearchPort = vectorSearchPort;
        _keywordSearchPort = keywordSearchPort;
        _fusionService = fusionService;
        _rerankerPort = rerankerPort;
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

        var vectorSearch = _vectorSearchPort.SearchAsync(
            query,
            embeddings[0],
            cancellationToken);
        var keywordSearch = _keywordSearchPort.SearchAsync(query, cancellationToken);
        await Task.WhenAll(vectorSearch, keywordSearch);

        IReadOnlyList<RetrievedChunk> candidates = _fusionService.Fuse(
            vectorSearch.Result,
            keywordSearch.Result);

        if (_rerankerPort is not null)
        {
            candidates = await _rerankerPort.RerankAsync(
                query,
                candidates,
                cancellationToken);
        }

        return candidates
            .Where(candidate => candidate.Score >= RelevanceThreshold)
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