using System.Text.Json;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>CreateWorkOrder</c> tool by persisting the work order and its safety prerequisites.
/// This is a gated write tool (TL-007) and must only be invoked after Supervisor approval (FR-019).
/// </summary>
public sealed class CreateWorkOrderExecutor(
    ISender sender,
    IEquipmentRepository equipmentRepository,
    ILogger<CreateWorkOrderExecutor> logger) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ToolName => "CreateWorkOrder";

    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var createRequest = JsonSerializer.Deserialize<CreateWorkOrderRequest>(request.ArgumentsJson, SerializerOptions);
            if (createRequest is null)
            {
                return new ToolDispatchResult(
                    request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                    "CreateWorkOrder_FAILED", "The CreateWorkOrder arguments could not be deserialized.");
            }

            // 1. Resolve Equipment details for the Work Order
            var equipment = await equipmentRepository.GetByIdAsync(createRequest.EquipmentId, cancellationToken);
            var equipmentName = equipment?.Name ?? "Unknown Equipment";
            var assetNumber = equipment?.SerialNumber;

            // Extract the user ID from the tool invocation context
            var createdBy = request.Context.UserId.ToString();

            // 2. Persist the Work Order via Application Command
            var createCommand = new CreateWorkOrderCommand(
                Title: createRequest.Title,
                Symptom: createRequest.Description,
                EquipmentName: equipmentName,
                CreatedBy: createdBy,
                EquipmentAssetNumber: assetNumber,
                ManualRevision: null,
                Location: null);

            var workOrderId = (Guid)await sender.Send(createCommand, cancellationToken);    
            
            // 3. Persist Safety Prerequisites via Application Command
            if (createRequest.SafetyPrerequisites is not null)
            {
                for (int i = 0; i < createRequest.SafetyPrerequisites.Count; i++)
                {
                    var safetyCommand = new AddSafetyPrerequisiteCommand(
                        WorkOrderId: workOrderId,
                        Description: createRequest.SafetyPrerequisites[i],
                        IsMandatory: true, 
                        SortOrder: i);
                    
                    await sender.Send(safetyCommand, cancellationToken);
                }
            }

            var response = new CreateWorkOrderResponse(true, "Created", workOrderId);
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
            logger.LogWarning(exception, "CreateWorkOrder received invalid JSON.");
            return new ToolDispatchResult(
                request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                "CreateWorkOrder_FAILED", $"The CreateWorkOrder arguments are invalid: {exception.Message}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CreateWorkOrder dispatch failed.");
            return new ToolDispatchResult(
                request.ToolName, false, ToolDispatchStatus.ExecutorFailed, null, 
                "CreateWorkOrder_FAILED", exception.Message);
        }
    }
}