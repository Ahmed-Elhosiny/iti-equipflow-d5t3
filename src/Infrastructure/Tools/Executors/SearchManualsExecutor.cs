using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes manual searches.
/// </summary>
public sealed class SearchManualsExecutor : IToolExecutor
{
    private readonly ILogger<SearchManualsExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchManualsExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record search requests.</param>
    public SearchManualsExecutor(ILogger<SearchManualsExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "SearchManuals";

    /// <inheritdoc />
    public Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var searchRequest = JsonSerializer.Deserialize<SearchManualsRequest>(request.ArgumentsJson, options)
            ?? throw new JsonException("Search manuals request could not be deserialized.");

        _logger.LogInformation(
            "Searching manuals for query {Query} and equipment {EquipmentId}.",
            searchRequest.Query,
            searchRequest.EquipmentId);

        var response = new SearchManualsResponse(
        [
            new SearchManualsResponse.Chunk(
                Guid.NewGuid(),
                "Dummy manual content for testing",
                0.95,
                new SearchManualsResponse.Citation(
                    Guid.Empty,
                    "Dummy manual",
                    null,
                    null))
        ]);
        var resultJson = JsonSerializer.Serialize(response);

        return Task.FromResult(
            new ToolDispatchResult(
                request.ToolName,
                true,
                ToolDispatchStatus.Success,
                resultJson,
                null,
                null));
    }
}
