using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Models;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Queries;
using EquipFlow.Application.Search.Services;
using EquipFlow.Domain.Search;
using NSubstitute;

namespace EquipFlow.Application.Tests.Search;

public sealed class SearchDocumentsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_FusesHybridResultsAndPreservesCitations()
    {
        // Arrange
        var embeddingPort = Substitute.For<IEmbeddingPort>();
        var vectorSearchPort = Substitute.For<IVectorSearchPort>();
        var keywordSearchPort = Substitute.For<IKeywordSearchPort>();
        var rerankerPort = Substitute.For<IRerankerPort>();
        var queryEmbedding = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]);
        var overlapChunkId = Guid.NewGuid();
        var vectorOnlyChunkId = Guid.NewGuid();
        var keywordOnlyChunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var vectorCitation = Citation.Create(documentId, "Maintenance Manual", 12, "Hydraulics");
        var keywordCitation = Citation.Create(documentId, "Maintenance Manual", 14, "Valves");
        var vectorResults = new List<RetrievedChunk>
        {
            new(overlapChunkId, documentId, "Overlapping result", 0.9, vectorCitation),
            new(vectorOnlyChunkId, documentId, "Vector-only result", 0.7, vectorCitation)
        };
        var keywordResults = new List<RetrievedChunk>
        {
            new(overlapChunkId, documentId, "Overlapping result", 0.8, keywordCitation),
            new(keywordOnlyChunkId, documentId, "Keyword-only result", 0.6, keywordCitation)
        };
        var filters = SearchFilters.Create(
            equipmentId: "pump-17",
            documentType: "maintenance",
            productionLine: "line-a",
            documentVersion: "v2");

        embeddingPort.GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReadOnlyMemory<float>[]>([queryEmbedding]));
        vectorSearchPort.SearchAsync(
                Arg.Any<SearchQuery>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RetrievedChunk>>(vectorResults));
        keywordSearchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RetrievedChunk>>(keywordResults));
        rerankerPort.RerankAsync(
                Arg.Any<SearchQuery>(),
                Arg.Any<IReadOnlyList<RetrievedChunk>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<IReadOnlyList<RetrievedChunk>>(1));

        var handler = new SearchDocumentsQueryHandler(
            embeddingPort,
            vectorSearchPort,
            keywordSearchPort,
            new ReciprocalRankFusionService(),
            rerankerPort);

        // Act
        var result = await handler.Handle(
            new SearchDocumentsQuery("hydraulic valve", 10, filters),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsRefusal);
        Assert.Equal(3, result.Results.Count);
        var overlapResult = Assert.Single(result.Results, item => item.ChunkId == overlapChunkId);
        Assert.Equal(vectorCitation, overlapResult.Citation);
        Assert.Contains(result.Results, item => item.ChunkId == vectorOnlyChunkId);
        Assert.Contains(result.Results, item => item.ChunkId == keywordOnlyChunkId);
        await embeddingPort.Received(1).GenerateEmbeddingsAsync(
            Arg.Is<IReadOnlyList<string>>(texts => texts.SequenceEqual(new[] { "hydraulic valve" })),
            Arg.Any<CancellationToken>());
        await vectorSearchPort.Received(1).SearchAsync(
            Arg.Is<SearchQuery>(query => query.QueryText == "hydraulic valve" && query.Filters == filters),
            queryEmbedding,
            Arg.Any<CancellationToken>());
        await keywordSearchPort.Received(1).SearchAsync(
            Arg.Is<SearchQuery>(query => query.QueryText == "hydraulic valve" && query.Filters == filters),
            Arg.Any<CancellationToken>());
        await rerankerPort.Received(1).RerankAsync(
            Arg.Any<SearchQuery>(),
            Arg.Is<IReadOnlyList<RetrievedChunk>>(chunks => chunks.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsRefusalWhenBothSearchesAreEmpty()
    {
        // Arrange
        var (handler, embeddingPort, vectorSearchPort, keywordSearchPort, _) = CreateHandler(
            Array.Empty<RetrievedChunk>(),
            Array.Empty<RetrievedChunk>());

        // Act
        var result = await handler.Handle(
            new SearchDocumentsQuery("missing procedure"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsRefusal);
        Assert.Empty(result.Results);
        Assert.Equal(
            "No sufficiently relevant documents found for the given query.",
            result.RefusalReason);
        await embeddingPort.Received(1).GenerateEmbeddingsAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
        await vectorSearchPort.Received(1).SearchAsync(
            Arg.Any<SearchQuery>(),
            Arg.Any<ReadOnlyMemory<float>>(),
            Arg.Any<CancellationToken>());
        await keywordSearchPort.Received(1).SearchAsync(
            Arg.Any<SearchQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesFiltersToBothSearchPorts()
    {
        // Arrange
        var filters = SearchFilters.Create(equipmentId: "compressor-4", documentType: "safety");
        var (handler, _, vectorSearchPort, keywordSearchPort, _) = CreateHandler(
            [CreateChunk()],
            [CreateChunk()]);

        // Act
        await handler.Handle(
            new SearchDocumentsQuery("lockout steps", 5, filters),
            CancellationToken.None);

        // Assert
        await vectorSearchPort.Received(1).SearchAsync(
            Arg.Is<SearchQuery>(query => query.Filters == filters),
            Arg.Any<ReadOnlyMemory<float>>(),
            Arg.Any<CancellationToken>());
        await keywordSearchPort.Received(1).SearchAsync(
            Arg.Is<SearchQuery>(query => query.Filters == filters),
            Arg.Any<CancellationToken>());
    }

    private static (
        SearchDocumentsQueryHandler Handler,
        IEmbeddingPort EmbeddingPort,
        IVectorSearchPort VectorSearchPort,
        IKeywordSearchPort KeywordSearchPort,
        IRerankerPort RerankerPort) CreateHandler(
        IReadOnlyList<RetrievedChunk> vectorResults,
        IReadOnlyList<RetrievedChunk> keywordResults)
    {
        var embeddingPort = Substitute.For<IEmbeddingPort>();
        var vectorSearchPort = Substitute.For<IVectorSearchPort>();
        var keywordSearchPort = Substitute.For<IKeywordSearchPort>();
        var rerankerPort = Substitute.For<IRerankerPort>();

        embeddingPort.GenerateEmbeddingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReadOnlyMemory<float>[]>([new ReadOnlyMemory<float>([1f])]));
        vectorSearchPort.SearchAsync(
                Arg.Any<SearchQuery>(),
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(vectorResults));
        keywordSearchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(keywordResults));

        return (
            new SearchDocumentsQueryHandler(
                embeddingPort,
                vectorSearchPort,
                keywordSearchPort,
                new ReciprocalRankFusionService(),
                rerankerPort),
            embeddingPort,
            vectorSearchPort,
            keywordSearchPort,
            rerankerPort);
    }

    private static RetrievedChunk CreateChunk() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Relevant search result",
        0.8,
        Citation.Create(Guid.NewGuid(), "Safety Manual", 3));
}