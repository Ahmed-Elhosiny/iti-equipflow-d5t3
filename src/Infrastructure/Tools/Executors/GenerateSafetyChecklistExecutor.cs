using System.Text.Json;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes safety checklist generation requests.
/// </summary>
public sealed class GenerateSafetyChecklistExecutor : IToolExecutor
{
    private readonly ILogger<GenerateSafetyChecklistExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateSafetyChecklistExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record safety checklist requests.</param>
    public GenerateSafetyChecklistExecutor(ILogger<GenerateSafetyChecklistExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public string ToolName => "GenerateSafetyChecklist";

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
        var checklistRequest = JsonSerializer.Deserialize<GenerateSafetyChecklistRequest>(
            request.ArgumentsJson,
            options)
            ?? throw new JsonException("Generate safety checklist request could not be deserialized.");

        _logger.LogInformation(
            "Generating safety checklist for equipment {EquipmentId} with {FaultCount} faults.",
            checklistRequest.EquipmentId,
            checklistRequest.Faults.Count);

        var response = new GenerateSafetyChecklistResponse(
        [
            new GenerateSafetyChecklistResponse.ChecklistItem(
                "Lockout/Tagout (LOTO) procedure applied",
                true),
            new GenerateSafetyChecklistResponse.ChecklistItem(
                "Wear appropriate PPE (safety goggles, gloves)",
                true),
            new GenerateSafetyChecklistResponse.ChecklistItem(
                "Verify zero energy state",
                true)
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
