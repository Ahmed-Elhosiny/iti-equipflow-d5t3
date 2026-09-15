using System.Diagnostics;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Application.Agentic.Events;

public static class AgentEventRecorder
{
    public static async Task<CompletionResult> CompleteAsync(
        ILLMProvider provider,
        CompletionRequest request,
        IAgentContext context,
        string agentName,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var completion = await provider.CompleteAsync(request, cancellationToken);

        if (context is IAgentEventCollector collector)
        {
            collector.Add(new LlmCallCompleted(
                ParseCorrelationId(context.CorrelationId),
                DateTimeOffset.UtcNow,
                agentName,
                stepIndex,
                "configured",
                request.Model ?? "unknown",
                completion.Usage.InputTokens,
                completion.Usage.OutputTokens,
                completion.Usage.TotalTokens,
                0m,
                stopwatch.ElapsedMilliseconds,
                false,
                null));
        }

        return completion;
    }

    public static async Task<ToolDispatchResult> DispatchAsync(
        IToolDispatcher dispatcher,
        ToolInvocationRequest request,
        IAgentContext context,
        string agentName,
        int stepIndex,
        string toolCallId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ToolDispatchResult? result = null;
        Exception? exception = null;

        try
        {
            result = await dispatcher.DispatchAsync(request, cancellationToken);
            return result;
        }
        catch (Exception caughtException)
        {
            exception = caughtException;
            throw;
        }
        finally
        {
            if (context is IAgentEventCollector collector)
            {
                collector.Add(new ToolInvoked(
                    ParseCorrelationId(context.CorrelationId),
                    DateTimeOffset.UtcNow,
                    agentName,
                    stepIndex,
                    request.ToolName,
                    toolCallId,
                    GetToolStatus(result, exception, cancellationToken),
                    stopwatch.ElapsedMilliseconds,
                    result?.ErrorMessage ?? exception?.Message,
                    request.ArgumentsJson,
                    result?.ResultJson));
            }
        }
    }

    private static ToolInvocationStatus GetToolStatus(
        ToolDispatchResult? result,
        Exception? exception,
        CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ToolInvocationStatus.Timeout
            : exception is not null || result is null || !result.Succeeded
                ? result?.Status is ToolDispatchStatus.BlockedByAuthorization
                    or ToolDispatchStatus.BlockedByAgentToolAllowList
                    or ToolDispatchStatus.BlockedByBudget
                    or ToolDispatchStatus.BlockedBySafetyGate
                    or ToolDispatchStatus.BlockedByValidation
                    or ToolDispatchStatus.ApprovalRequired
                    ? ToolInvocationStatus.Denied
                    : ToolInvocationStatus.Failed
                : ToolInvocationStatus.Success;

    private static Guid ParseCorrelationId(string correlationId) =>
        Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.Empty;
}