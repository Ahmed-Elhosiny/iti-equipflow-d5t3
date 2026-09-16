using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>create_work_order</c> tool.
/// </summary>
/// <param name="sender">The mediator used to create work orders.</param>
/// <param name="logger">The logger used to record execution failures.</param>
public sealed class CreateWorkOrderExecutor(
    ISender sender,
    ILogger<CreateWorkOrderExecutor> logger) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the name of the tool handled by this executor.
    /// </summary>
    public string ToolName => "create_work_order";

    /// <summary>
    /// Deserializes the tool arguments and creates a work order through the application layer.
    /// </summary>
    /// <param name="arguments">The JSON arguments supplied to the tool.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <returns>A successful result containing the new work order identifier, or a failure result.</returns>
    public async Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(arguments, null, cancellationToken);

    private async Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = arguments.Deserialize<CreateWorkOrderRequest>(SerializerOptions);
            if (request is null)
            {
                return new ToolExecutionResult(
                    false,
                    null,
                    "The create_work_order arguments could not be deserialized.");
            }

            var command = new CreateWorkOrderCommand(
                request.Title,
                request.Description,
                request.EquipmentId.ToString(),
                userId ?? string.Empty);
            var workOrderResult = await sender.Send(command, cancellationToken);
            if (workOrderResult is not Guid workOrderId)
            {
                throw new InvalidOperationException(
                    "The create_work_order handler did not return a work order identifier.");
            }

            return new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(new CreateWorkOrderResponse(workOrderId, "Created")),
                null);
        }
        catch (JsonException exception)
        {
            return new ToolExecutionResult(
                false,
                null,
                $"The create_work_order arguments are invalid: {exception.Message}");
        }
        catch (ValidationException exception)
        {
            return new ToolExecutionResult(false, null, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CreateWorkOrder execution failed for user {UserId}.", userId);
            return new ToolExecutionResult(false, null, exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = JsonDocument.Parse(request.ArgumentsJson);
            var result = await ExecuteAsync(
                document.RootElement,
                request.Context.UserId.ToString(),
                cancellationToken);

            return new ToolDispatchResult(
                request.ToolName,
                result.Succeeded,
                result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
                result.Result,
                result.Succeeded ? null : "CREATE_WORK_ORDER_FAILED",
                result.Error);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "CreateWorkOrder received invalid JSON.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "CREATE_WORK_ORDER_FAILED",
                $"The create_work_order arguments are invalid: {exception.Message}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CreateWorkOrder dispatch failed.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "CREATE_WORK_ORDER_FAILED",
                exception.Message);
        }
    }
}