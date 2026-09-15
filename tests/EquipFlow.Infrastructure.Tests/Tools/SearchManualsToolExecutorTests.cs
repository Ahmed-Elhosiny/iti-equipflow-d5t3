using System.Text.Json;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain.Search;
using EquipFlow.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Application.Tests.Tools;

public sealed class SearchManualsToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_MapsSearchResultsIncludingCitations()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<SearchManualsToolExecutor>>();
        var chunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var results = new[]
        {
            SearchResult.Create(
                chunkId,
                documentId,
                "Lock out the pump before servicing.",
                0.91,
                1,
                Citation.Create(documentId, "Pump Maintenance Manual", 42, "Safety"))
        };
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchResult>>(results));
        var executor = new SearchManualsToolExecutor(searchPort, logger);

        var response = await executor.ExecuteAsync(
            new SearchManualsRequest(null, "pump lockout", "maintenance"));

        var chunk = Assert.Single(response.Chunks);
        Assert.Equal(chunkId, chunk.ChunkId);
        Assert.Equal("Lock out the pump before servicing.", chunk.Content);
        Assert.Equal(0.91, chunk.Score);
        Assert.Equal(documentId, chunk.Citation.DocumentId);
        Assert.Equal("Pump Maintenance Manual", chunk.Citation.DocumentTitle);
        Assert.Equal(42, chunk.Citation.Page);
        Assert.Equal("Safety", chunk.Citation.Section);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResultsAreEmpty_LogsWarningAndReturnsEmptyResponse()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<SearchManualsToolExecutor>>();
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>()));
        var executor = new SearchManualsToolExecutor(searchPort, logger);

        var response = await executor.ExecuteAsync(
            new SearchManualsRequest(null, "unknown procedure", null));

        Assert.Empty(response.Chunks);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSearchPortThrows_LogsWarningAndRethrows()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<SearchManualsToolExecutor>>();
        var exception = new InvalidOperationException("Search backend unavailable.");
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<SearchResult>>(exception));
        var executor = new SearchManualsToolExecutor(searchPort, logger);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            new SearchManualsRequest(null, "pump lockout", null)));

        Assert.Same(exception, thrown);
        Assert.Equal(1, WarningLogCount(logger));
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesValidJsonAndMapsResponse()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<SearchManualsToolExecutor>>();
        var chunkId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchResult>>(
            [SearchResult.Create(
                chunkId,
                documentId,
                "Check the pressure gauge.",
                0.78,
                1,
                Citation.Create(documentId, "Pressure Manual", 7, "Inspection"))]));
        var executor = new SearchManualsToolExecutor(searchPort, logger);
        var request = new ToolInvocationRequest(
            "SearchManuals",
            $$"""{"equipmentId":"{{Guid.NewGuid()}}","query":"pressure gauge","documentType":"inspection"}""",
            CreateContext());

        var dispatchResult = await executor.ExecuteAsync(request);

        Assert.True(dispatchResult.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, dispatchResult.Status);
        var response = JsonSerializer.Deserialize<SearchManualsResponse>(dispatchResult.ResultJson!);
        var chunk = Assert.Single(response!.Chunks);
        Assert.Equal(chunkId, chunk.ChunkId);
        Assert.Equal("Check the pressure gauge.", chunk.Content);
        Assert.Equal(0.78, chunk.Score);
        Assert.Equal(documentId, chunk.Citation.DocumentId);
        Assert.Equal("Pressure Manual", chunk.Citation.DocumentTitle);
        Assert.Equal(7, chunk.Citation.Page);
        Assert.Equal("Inspection", chunk.Citation.Section);
    }

    [Fact]
    public async Task ExecuteAsync_DispatchWithInvalidJson_ThrowsJsonException()
    {
        var executor = new SearchManualsToolExecutor(
            Substitute.For<ISearchPort>(),
            Substitute.For<ILogger<SearchManualsToolExecutor>>());
        var request = new ToolInvocationRequest(
            "SearchManuals",
            "{ invalid json",
            CreateContext());

        await Assert.ThrowsAsync<JsonException>(() => executor.ExecuteAsync(request));
    }

    private static ToolInvocationContext CreateContext() => new(
        Guid.NewGuid(),
        "Technician",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "DiagnosticAgent",
        Guid.NewGuid().ToString());

    private static int WarningLogCount(ILogger<SearchManualsToolExecutor> logger) =>
        logger.ReceivedCalls().Count(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel.Warning);
}