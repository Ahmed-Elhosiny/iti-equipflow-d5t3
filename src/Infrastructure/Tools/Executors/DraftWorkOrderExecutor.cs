using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>DraftWorkOrder</c> tool.
/// </summary>
public sealed class DraftWorkOrderExecutor(
    ILogger<DraftWorkOrderExecutor> logger) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ToolName => "DraftWorkOrder";

    public Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = arguments.Deserialize<DraftWorkOrderRequest>(SerializerOptions);
            if (request is null)
            {
                return Task.FromResult(new ToolExecutionResult(
                    false,
                    null,
                    "The DraftWorkOrder arguments could not be deserialized."));
            }

            // No DB writes. Just acknowledge the draft was successfully formulated.
            var response = new DraftWorkOrderResponse(true, "Drafted");
            return Task.FromResult(new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(response),
                null));
        }
        catch (JsonException exception)
        {
            return Task.FromResult(new ToolExecutionResult(
                false,
                null,
                $"The DraftWorkOrder arguments are invalid: {exception.Message}"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "DraftWorkOrder execution failed.");
            return Task.FromResult(new ToolExecutionResult(false, null, exception.Message));
        }
    }

    public async Task<ToolDispatchResult> ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = JsonDocument.Parse(request.ArgumentsJson);
            var result = await ExecuteAsync(document.RootElement, cancellationToken);

            return new ToolDispatchResult(
                request.ToolName,
                result.Succeeded,
                result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
                result.Result,
                result.Succeeded ? null : "DraftWorkOrder_FAILED",
                result.Error);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "DraftWorkOrder received invalid JSON.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "DraftWorkOrder_FAILED",
                $"The DraftWorkOrder arguments are invalid: {exception.Message}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "DraftWorkOrder dispatch failed.");
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "DraftWorkOrder_FAILED",
                exception.Message);
        }
    }
}