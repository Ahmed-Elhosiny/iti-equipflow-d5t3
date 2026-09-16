using System.Text.Json;
using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain.Search;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes safety checklist generation requests.
/// </summary>
public sealed class GenerateSafetyChecklistExecutor : IToolExecutor
{
    private readonly ISearchPort _searchPort;
    private readonly ILogger<GenerateSafetyChecklistExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateSafetyChecklistExecutor"/> class.
    /// </summary>
    /// <param name="searchPort">The search port used to retrieve safety procedures.</param>
    /// <param name="logger">The logger used to record safety checklist requests.</param>
    public GenerateSafetyChecklistExecutor(
        ISearchPort searchPort,
        ILogger<GenerateSafetyChecklistExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(searchPort);
        ArgumentNullException.ThrowIfNull(logger);
        _searchPort = searchPort;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "GenerateSafetyChecklist";

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
            var checklistRequest = JsonSerializer.Deserialize<GenerateSafetyChecklistRequest>(
                request.ArgumentsJson,
                options)
                ?? throw new JsonException("Generate safety checklist request could not be deserialized.");

            _logger.LogInformation(
                "Generating safety checklist for equipment {EquipmentId} and task {TaskDescription}.",
                checklistRequest.EquipmentId,
                checklistRequest.TaskDescription);

            var results = await _searchPort.SearchAsync(
                SearchQuery.Create(
                    checklistRequest.TaskDescription,
                    filters: SearchFilters.Create(
                        checklistRequest.EquipmentId.ToString(),
                        "SafetyProcedure")),
                cancellationToken);

            if (results.Count == 0)
            {
                _logger.LogWarning(
                    "No safety procedures found for equipment {EquipmentId} and task {TaskDescription}.",
                    checklistRequest.EquipmentId,
                    checklistRequest.TaskDescription);
            }

            var response = new GenerateSafetyChecklistResponse(
                results.Select(result => new GenerateSafetyChecklistResponse.ChecklistItem(
                    result.Content,
                    true,
                    new GenerateSafetyChecklistResponse.ChecklistCitation(
                        result.Citation.DocumentId,
                        result.Citation.DocumentTitle,
                        result.Citation.Page,
                        result.Citation.Section))).ToList());

            return new ToolDispatchResult(
                request.ToolName,
                true,
                ToolDispatchStatus.Success,
                JsonSerializer.Serialize(response),
                null,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Safety checklist arguments could not be deserialized.");
            return FailedResult(request, exception);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Safety checklist generation failed for tool request {ToolName}.",
                request.ToolName);
            return FailedResult(request, exception);
        }
    }

    private static ToolDispatchResult FailedResult(
        ToolInvocationRequest request,
        Exception exception) =>
        new(
            request.ToolName,
            false,
            ToolDispatchStatus.ExecutorFailed,
            null,
            "GENERATE_SAFETY_CHECKLIST_FAILED",
            exception.Message);
}
