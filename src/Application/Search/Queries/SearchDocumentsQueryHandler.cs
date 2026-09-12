using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Services;
using EquipFlow.Domain.Search;
using MediatR;

namespace EquipFlow.Application.Search.Queries;

public sealed class SearchDocumentsQueryHandler
    : IRequestHandler<SearchDocumentsQuery, SearchDocumentsQueryResult>
{
    private const double RelevanceThreshold = 0.0;
    private const string RefusalReason =
        "No sufficiently relevant documents found for the given query.";

    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorSearchPort _vectorSearchPort;
    private readonly IKeywordSearchPort _keywordSearchPort;
    private readonly IRerankerPort? _rerankerPort;

    public SearchDocumentsQueryHandler(
        IEmbeddingPort embeddingPort,
        IVectorSearchPort vectorSearchPort,
        IKeywordSearchPort keywordSearchPort,
        IRerankerPort? rerankerPort = null)
    {
        _embeddingPort = embeddingPort;
        _vectorSearchPort = vectorSearchPort;
        _keywordSearchPort = keywordSearchPort;
        _rerankerPort = rerankerPort;
    }

    public async Task<SearchDocumentsQueryResult> Handle(
        SearchDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = SearchQuery.Create(request.QueryText, request.TopK, request.Filters);
        var embeddings = await _embeddingPort.GenerateEmbeddingsAsync(
            [request.QueryText],
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

        IReadOnlyList<RetrievedChunk> candidates = ReciprocalRankFusionService.Fuse(
            vectorSearch.Result,
            keywordSearch.Result);

        if (_rerankerPort is not null)
        {
            candidates = await _rerankerPort.RerankAsync(
                query,
                candidates,
                cancellationToken);
        }

        var relevantCandidates = candidates
            .Where(candidate => candidate.Score >= RelevanceThreshold)
            .Take(request.TopK)
            .ToList();

        if (relevantCandidates.Count == 0)
        {
            return SearchDocumentsQueryResult.Refused(RefusalReason);
        }

        var results = relevantCandidates
            .Select((candidate, index) => SearchResult.Create(
                candidate.ChunkId,
                candidate.DocumentId,
                candidate.Content,
                candidate.Score,
                index + 1,
                candidate.Citation))
            .ToList();

        return SearchDocumentsQueryResult.Success(results);
    }
}