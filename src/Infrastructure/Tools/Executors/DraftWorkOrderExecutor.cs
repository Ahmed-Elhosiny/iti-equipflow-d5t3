using System.Text.Json;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Domain;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>DraftWorkOrder</c> tool by composing the work order details in-memory.
/// This tool is read-only and does NOT persist the work order to the database (compose-only).
/// </summary>
public sealed class DraftWorkOrderExecutor(
    IEquipmentRepository equipmentRepository,
    ILogger<DraftWorkOrderExecutor> logger) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ToolName => "DraftWorkOrder";

    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var draftRequest = JsonSerializer.Deserialize<DraftWorkOrderRequest>(request.ArgumentsJson, SerializerOptions);
            if (draftRequest is null)
            {
                return new ToolDispatchResult(
                    request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                    "DraftWorkOrder_FAILED", "The DraftWorkOrder arguments could not be deserialized.");
            }

            // 1. Resolve Equipment details to ensure the equipment exists
            Equipment? equipment = null;

            if (Guid.TryParse(draftRequest.EquipmentId, out var parsedId))
            {
                equipment = await equipmentRepository.GetByIdAsync(parsedId, cancellationToken);
            }

            if (equipment is null)
            {
                var allEquipment = await equipmentRepository.GetAllAsync(null, cancellationToken);
                equipment = allEquipment.FirstOrDefault(e => 
                    e.Name.Equals(draftRequest.EquipmentId, StringComparison.OrdinalIgnoreCase));
            }

            if (equipment is null)
            {
                 return new ToolDispatchResult(
                    request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                    "DraftWorkOrder_FAILED", $"Equipment '{draftRequest.EquipmentId}' not found.");
            }

            // 2. Compose the response without persisting to the database
            var response = new DraftWorkOrderResponse(true, "Composed", null);
            
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
            logger.LogWarning(exception, "DraftWorkOrder received invalid JSON.");
            return new ToolDispatchResult(
                request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                "DraftWorkOrder_FAILED", $"The DraftWorkOrder arguments are invalid: {exception.Message}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "DraftWorkOrder dispatch failed.");
            return new ToolDispatchResult(
                request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                "DraftWorkOrder_FAILED", exception.Message);
        }
    }
}