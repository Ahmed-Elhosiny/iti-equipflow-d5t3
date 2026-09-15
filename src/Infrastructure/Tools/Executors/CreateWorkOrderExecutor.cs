using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using EquipFlow.Application.WorkOrders.Commands;
using MediatR;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>create_work_order</c> tool.
/// </summary>
/// <param name="workOrderCommandPort">The application port used to create work orders.</param>
public sealed class CreateWorkOrderExecutor(ISender sender) : IToolExecutor
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
                request.EquipmentId.ToString());
            var workOrderId = (Guid)(await sender.Send(command, cancellationToken))!;

            return new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(new { WorkOrderId = workOrderId }),
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

        using var document = JsonDocument.Parse(request.ArgumentsJson);
        var result = await ExecuteAsync(document.RootElement, cancellationToken);

        return new ToolDispatchResult(
            request.ToolName,
            result.Succeeded,
            result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
            result.Result,
            result.Succeeded ? null : "CREATE_WORK_ORDER_FAILED",
            result.Error);
    }
}