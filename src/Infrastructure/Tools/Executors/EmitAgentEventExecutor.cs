using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>emit_agent_event</c> tool.
/// </summary>
/// <param name="logger">The logger used to record agent events.</param>
public sealed class EmitAgentEventExecutor(ILogger<EmitAgentEventExecutor> logger) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the name of the tool handled by this executor.
    /// </summary>
    public string ToolName => "emit_agent_event";

    /// <summary>
    /// Deserializes the tool arguments and records the agent event.
    /// </summary>
    /// <param name="arguments">The JSON arguments supplied to the tool.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <returns>A successful result confirming the event was recorded, or a failure result.</returns>
    public Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(arguments, null, cancellationToken);

    private Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        string? _,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = arguments.Deserialize<EmitAgentEventRequest>(SerializerOptions);
            if (request is null)
            {
                return Task.FromResult(new ToolExecutionResult(
                    false,
                    null,
                    "The emit_agent_event arguments could not be deserialized."));
            }

            if (string.IsNullOrWhiteSpace(request.AgentName)
                || string.IsNullOrWhiteSpace(request.EventName)
                || string.IsNullOrWhiteSpace(request.Details))
            {
                throw new ValidationException(
                    "The emit_agent_event arguments must include agentName, eventName, and details.");
            }

            logger.LogInformation(
                "Agent event emitted: {EventName}, Payload: {Payload}",
                request.EventName,
                request.Details);

            return Task.FromResult(new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(new EmitAgentEventResponse(true)),
                null));
        }
        catch (JsonException exception)
        {
            return Task.FromResult(new ToolExecutionResult(
                false,
                null,
                $"The emit_agent_event arguments are invalid: {exception.Message}"));
        }
        catch (ValidationException exception)
        {
            return Task.FromResult(new ToolExecutionResult(false, null, exception.Message));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new ToolExecutionResult(false, null, exception.Message));
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
            var result = await ExecuteAsync(document.RootElement, cancellationToken);

            return new ToolDispatchResult(
                request.ToolName,
                result.Succeeded,
                result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
                result.Result,
                result.Succeeded ? null : "EMIT_AGENT_EVENT_FAILED",
                result.Error);
        }
        catch (JsonException exception)
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "EMIT_AGENT_EVENT_FAILED",
                $"The emit_agent_event arguments are invalid: {exception.Message}");
        }
        catch (ValidationException exception)
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "EMIT_AGENT_EVENT_FAILED",
                exception.Message);
        }
        catch (Exception exception)
        {
            return new ToolDispatchResult(
                request.ToolName,
                false,
                ToolDispatchStatus.ExecutorFailed,
                null,
                "EMIT_AGENT_EVENT_FAILED",
                exception.Message);
        }
    }
}