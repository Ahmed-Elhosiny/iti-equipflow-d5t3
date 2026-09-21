using System.Text.Json;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain.Search;
using EquipFlow.Infrastructure.Tools.Executors;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EquipFlow.Infrastructure.Tests.Tools;

public sealed class GenerateSafetyChecklistExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSearchReturnsResults_MapsItemsAndCitations()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<GenerateSafetyChecklistExecutor>>();
        var documentId = Guid.NewGuid();
        SearchQuery? capturedQuery = null;
        var results = new[]
        {
            SearchResult.Create(
                Guid.NewGuid(),
                documentId,
                "Isolate the power supply before servicing.",
                0.94,
                1,
                Citation.Create(documentId, "Pump Safety Procedure", 12, "Isolation"))
        };
        searchPort.SearchAsync(Arg.Do<SearchQuery>(query => capturedQuery = query), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchResult>>(results));
        var executor = new GenerateSafetyChecklistExecutor(searchPort, logger);

        var result = await executor.ExecuteAsync(CreateRequest(Guid.NewGuid(), "Replace the pump motor"));

        Assert.True(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.Success, result.Status);
        var response = JsonSerializer.Deserialize<GenerateSafetyChecklistResponse>(result.ResultJson!);
        var item = Assert.Single(response!.Items);
        Assert.Equal("Isolate the power supply before servicing.", item.Description);
        Assert.True(item.IsMandatory);
        Assert.Equal(documentId, item.Citation.DocumentId);
        Assert.Equal("Pump Safety Procedure", item.Citation.DocumentTitle);
        Assert.Equal(12, item.Citation.Page);
        Assert.Equal("Isolation", item.Citation.Section);

        await searchPort.Received(1).SearchAsync(
            Arg.Any<SearchQuery>(),
            Arg.Any<CancellationToken>());
        Assert.Equal("Replace the pump motor", capturedQuery!.QueryText);
        Assert.Equal("SafetyProcedure", capturedQuery.Filters.DocumentType);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSearchReturnsEmpty_ReturnsEmptyItems()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<GenerateSafetyChecklistExecutor>>();
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>()));
        var executor = new GenerateSafetyChecklistExecutor(searchPort, logger);

        var result = await executor.ExecuteAsync(CreateRequest(Guid.NewGuid(), "Inspect the valve"));

        Assert.True(result.Succeeded);
        var response = JsonSerializer.Deserialize<GenerateSafetyChecklistResponse>(result.ResultJson!);
        Assert.Empty(response!.Items);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSearchThrows_ReturnsExecutorFailed()
    {
        var searchPort = Substitute.For<ISearchPort>();
        var logger = Substitute.For<ILogger<GenerateSafetyChecklistExecutor>>();
        var exception = new InvalidOperationException("Search backend unavailable.");
        searchPort.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<SearchResult>>(exception));
        var executor = new GenerateSafetyChecklistExecutor(searchPort, logger);

        var result = await executor.ExecuteAsync(CreateRequest(Guid.NewGuid(), "Inspect the valve"));

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("GENERATE_SAFETY_CHECKLIST_FAILED", result.ErrorCode);
        Assert.Equal(exception.Message, result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsAreInvalidJson_ReturnsExecutorFailed()
    {
        var logger = Substitute.For<ILogger<GenerateSafetyChecklistExecutor>>();
        var executor = new GenerateSafetyChecklistExecutor(
            Substitute.For<ISearchPort>(),
            logger);
        var request = new ToolInvocationRequest(
            "GenerateSafetyChecklist",
            "{ invalid json",
            CreateContext());

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolDispatchStatus.ExecutorFailed, result.Status);
        Assert.Equal("GENERATE_SAFETY_CHECKLIST_FAILED", result.ErrorCode);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolInvocationRequest CreateRequest(Guid equipmentId, string taskDescription) =>
        new(
            "GenerateSafetyChecklist",
            JsonSerializer.Serialize(new GenerateSafetyChecklistRequest(equipmentId.ToString(), taskDescription)),
            CreateContext());

    private static ToolInvocationContext CreateContext() => new(
        Guid.NewGuid(),
        "Technician",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "DiagnosticSafetyPlanner",
        Guid.NewGuid().ToString());
}