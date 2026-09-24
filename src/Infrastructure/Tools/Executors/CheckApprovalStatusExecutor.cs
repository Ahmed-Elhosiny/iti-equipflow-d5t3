using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Queries;
using MediatR;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>check_approval_status</c> tool.
/// </summary>
/// <param name="sender">The mediator used to query the work order.</param>
public sealed class CheckApprovalStatusExecutor(ISender sender) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the name of the tool handled by this executor.
    /// </summary>
    public string ToolName => "check_approval_status";

    /// <summary>
    /// Deserializes the tool arguments and retrieves the work order approval status.
    /// </summary>
    /// <param name="arguments">The JSON arguments supplied to the tool.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <returns>A successful result containing the approval status, or a failure result.</returns>
    public Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(arguments, null, cancellationToken);

    private async Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = arguments.Deserialize<CheckApprovalStatusRequest>(SerializerOptions);
            if (request is null)
            {
                return new ToolExecutionResult(
                    false,
                    null,
                    "The check_approval_status arguments could not be deserialized.");
            }

            var workOrder = await sender.Send(
                new GetWorkOrderByIdQuery(request.WorkOrderId, userId ?? string.Empty),
                cancellationToken);
            if (workOrder is null)
            {
                return new ToolExecutionResult(
                    false,
                    null,
                    $"Work order '{request.WorkOrderId}' was not found.");
            }

            return new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(new CheckApprovalStatusResponse(
                    workOrder.Status.ToString(),
                    workOrder.DecisionBy,
                    workOrder.DecisionAtUtc?.UtcDateTime)),
                null);
        }
        catch (JsonException exception)
        {
            return new ToolExecutionResult(
                false,
                null,
                $"The check_approval_status arguments are invalid: {exception.Message}");
        }
        catch (ValidationException exception)
        {
            return new ToolExecutionResult(false, null, exception.Message);
        }
        catch (Exception exception)
        {
            return new ToolExecutionResult(false, null, exception.Message);
        }
    }

    /// <inheritdoc />
    async Task<ToolDispatchResult> IToolExecutor.ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = JsonDocument.Parse(request.ArgumentsJson);
            // Extract the UserId from the ToolInvocationContext to satisfy the object-level authorization check
            var result = await ExecuteAsync(document.RootElement, request.Context.UserId.ToString(), cancellationToken);

            return new ToolDispatchResult(
                request.ToolName,
                result.Succeeded,
                result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
                result.Result,
                result.Succeeded ? null : "CHECK_APPROVAL_STATUS_FAILED",
                result.Error);
        }
        catch (JsonException exception)
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "CHECK_APPROVAL_STATUS_FAILED",
                $"The check_approval_status arguments are invalid: {exception.Message}");
        }
    }
}