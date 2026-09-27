using System.Text.Json;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>DraftWorkOrder</c> tool by persisting the work order and its safety prerequisites.
/// </summary>
public sealed class DraftWorkOrderExecutor(
    ISender sender,
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

            // 1. Resolve Equipment details for the Work Order
            var equipment = await equipmentRepository.GetByIdAsync(draftRequest.EquipmentId, cancellationToken);
            var equipmentName = equipment?.Name ?? "Unknown Equipment";
            var assetNumber = equipment?.SerialNumber;

            // Extract the user ID from the tool invocation context (Technician who initiated the run)
            var createdBy = request.Context.UserId.ToString();

            // 2. Persist the Work Order via Application Command
            var createCommand = new CreateWorkOrderCommand(
                Title: draftRequest.Title,
                Symptom: draftRequest.Description,
                EquipmentName: equipmentName,
                CreatedBy: createdBy,
                EquipmentAssetNumber: assetNumber,
                ManualRevision: null,
                Location: null);

            var workOrderId = (Guid)await sender.Send(createCommand, cancellationToken);    
            
            // 3. Persist Safety Prerequisites via Application Command
            if (draftRequest.SafetyPrerequisites is not null)
            {
                for (int i = 0; i < draftRequest.SafetyPrerequisites.Count; i++)
                {
                    var safetyCommand = new AddSafetyPrerequisiteCommand(
                        WorkOrderId: workOrderId,
                        Description: draftRequest.SafetyPrerequisites[i],
                        IsMandatory: true, // Safety prerequisites from the agent are mandatory
                        SortOrder: i);
                    
                    await sender.Send(safetyCommand, cancellationToken);
                }
            }

            var response = new DraftWorkOrderResponse(true, "Drafted", workOrderId);
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