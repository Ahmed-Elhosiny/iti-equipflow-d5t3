using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes fault history queries.
/// </summary>
public sealed class QueryFaultHistoryExecutor : IToolExecutor
{
    private readonly ILogger<QueryFaultHistoryExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFaultHistoryExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record fault history queries.</param>
    public QueryFaultHistoryExecutor(ILogger<QueryFaultHistoryExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "QueryFaultHistory";

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
        var historyRequest = JsonSerializer.Deserialize<QueryFaultHistoryRequest>(request.ArgumentsJson, options)
            ?? throw new JsonException("Query fault history request could not be deserialized.");

        _logger.LogInformation(
            "Querying fault history for equipment {EquipmentId} and symptom {Symptom}.",
            historyRequest.EquipmentId,
            historyRequest.Symptom);

        var response = new QueryFaultHistoryResponse(
        [
            new QueryFaultHistoryResponse.HistoryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow.AddDays(-30),
                "Dummy historical fault for testing",
                "Replaced sensor")
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
