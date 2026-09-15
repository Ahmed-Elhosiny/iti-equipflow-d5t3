using System.Text.Json;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain.Search;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools;

/// <summary>
/// Adapts the application search port to the SearchManuals tool contract.
/// </summary>
public sealed class SearchManualsToolExecutor : ISearchManualsTool, IToolExecutor
{
    private readonly ISearchPort _searchPort;
    private readonly ILogger<SearchManualsToolExecutor> _logger;

    public SearchManualsToolExecutor(
        ISearchPort searchPort,
        ILogger<SearchManualsToolExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(searchPort);
        ArgumentNullException.ThrowIfNull(logger);

        _searchPort = searchPort;
        _logger = logger;
    }

    public string ToolName => "SearchManuals";

    public async Task<SearchManualsResponse> ExecuteAsync(
        SearchManualsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var results = await _searchPort.SearchAsync(
                SearchQuery.Create(
                    request.Query,
                    filters: SearchFilters.Create(
                        request.EquipmentId?.ToString(),
                        request.DocumentType)),
                cancellationToken);

            if (results.Count == 0)
            {
                _logger.LogWarning(
                    "Manual search returned no results for query {Query} and equipment {EquipmentId}.",
                    request.Query,
                    request.EquipmentId);
            }

            return new SearchManualsResponse(
                results.Select(result => new SearchManualsResponse.Chunk(
                    result.ChunkId,
                    result.Content,
                    result.Score,
                    new SearchManualsResponse.Citation(
                        result.Citation.DocumentId,
                        result.Citation.DocumentTitle,
                        result.Citation.Page,
                        result.Citation.Section))).ToList());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Manual search failed for query {Query} and equipment {EquipmentId}.",
                request.Query,
                request.EquipmentId);
            throw;
        }
    }

    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var searchRequest = JsonSerializer.Deserialize<SearchManualsRequest>(
            request.ArgumentsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Search manuals request could not be deserialized.");

        var response = await ExecuteAsync(searchRequest, cancellationToken);
        return new ToolDispatchResult(
            request.ToolName,
            true,
            ToolDispatchStatus.Success,
            JsonSerializer.Serialize(response),
            null,
            null);
    }
}