using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes fault history queries.
/// </summary>
public sealed class QueryFaultHistoryExecutor : IToolExecutor
{
    private readonly IWorkOrderRepository _repository;
    private readonly ILogger<QueryFaultHistoryExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFaultHistoryExecutor"/> class.
    /// </summary>
    /// <param name="repository">The repository used to retrieve historical work order data.</param>
    /// <param name="logger">The logger used to record fault history queries.</param>
    public QueryFaultHistoryExecutor(
        IWorkOrderRepository repository,
        ILogger<QueryFaultHistoryExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "QueryFaultHistory";

    /// <inheritdoc />
    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
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

            var workOrder = await _repository.GetByIdAsync(historyRequest.EquipmentId, cancellationToken);
            var matchesSymptom = workOrder is not null
                && (string.IsNullOrWhiteSpace(historyRequest.Symptom)
                    || workOrder.Symptom.Contains(historyRequest.Symptom, StringComparison.OrdinalIgnoreCase));

            var response = new QueryFaultHistoryResponse(
                matchesSymptom
                    ?
                    [
                        new QueryFaultHistoryResponse.HistoryRecord(
                            workOrder!.Id,
                            workOrder.CreatedAtUtc.UtcDateTime,
                            workOrder.Symptom,
                            workOrder.DecisionComment)
                    ]
                    : []);

            if (response.Records.Count == 0)
            {
                _logger.LogWarning(
                    "No fault history found for equipment {EquipmentId} and symptom {Symptom}.",
                    historyRequest.EquipmentId,
                    historyRequest.Symptom);
            }

            return new ToolDispatchResult(
                request.ToolName,
                true,
                ToolDispatchStatus.Success,
                JsonSerializer.Serialize(response),
                null,
                null);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Fault history query arguments could not be deserialized.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "QUERY_FAULT_HISTORY_FAILED",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Fault history query failed for tool request {ToolName}.",
                request.ToolName);
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "QUERY_FAULT_HISTORY_FAILED",
                exception.Message);
        }
    }
}
