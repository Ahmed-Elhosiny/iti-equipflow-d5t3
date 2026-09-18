using System.Diagnostics;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Application.Agentic.Orchestration;

public sealed class SequentialSupervisorOrchestrator(
    IAgent<SymptomMatchInput, SymptomMatchOutput> symptomMatcher,
    IAgent<DiagnosticPlanInput, DiagnosticPlanOutput> diagnosticPlanner,
    IAgent<WorkOrderInput, WorkOrderOutput> workOrderGenerator,
    ICostGovernor costGovernor,
    IAgentEventStore agentEventStore,
    ILogger<SequentialSupervisorOrchestrator> logger)
{
    private const string MockUserId = "00000000-0000-0000-0000-000000000001";
    private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan WorkflowTimeout = TimeSpan.FromSeconds(120);

    public async Task<WorkflowResult> RunWorkflowAsync(
        MaintenanceRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.TryParse(context.CorrelationId, out var parsedCorrelationId)
            ? parsedCorrelationId
            : Guid.NewGuid();
        var collectedEvents = new List<AgentEventBase>();
        var workflowStopwatch = Stopwatch.StartNew();
        var eventCollector = new RecordingAgentContext(context, correlationId, collectedEvents);
        var finalStatus = AgentRunStatus.Failed;
        string? finalError = null;
        string? outputSummary = null;
        Guid? reservationId = null;
        var resolvedUserId = string.IsNullOrWhiteSpace(context.UserId)
            ? string.IsNullOrWhiteSpace(request.UserId) ? MockUserId : request.UserId
            : context.UserId;

        collectedEvents.Add(new AgentRunStarted(
            correlationId,
            DateTimeOffset.UtcNow,
            nameof(SequentialSupervisorOrchestrator),
            0,
            request.SymptomDescription,
            (int)WorkflowTimeout.TotalMilliseconds));

        using var workflowTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workflowTimeout.CancelAfter(WorkflowTimeout);
        var workflowToken = workflowTimeout.Token;

        try
        {
            // TODO: Extract the user identity from the authenticated user context.
            var reservation = await costGovernor.EstimateAndReserveAsync(
                resolvedUserId,
                estimatedTokens: 3000,
                pricePerThousandTokens: 0.01m,
                workflowToken);

            if (reservation.Status == CostGovernorStatus.Blocked)
            {
                finalError = reservation.Reason;
                return WorkflowResult.Blocked(
                    reservation.Reason,
                    reservation.EstimatedCost,
                    reservation.RemainingBudget);
            }

            if (reservation.Status == CostGovernorStatus.Cached)
            {
                finalStatus = AgentRunStatus.Success;
                outputSummary = "Semantic cache hit.";
                return WorkflowResult.Cached(reservation.CachedResponse!);
            }

            reservationId = reservation.ReservationId!.Value;

            var symptomResult = await ExecuteStepAsync(
                symptomMatcher,
                new SymptomMatchInput(request.SymptomDescription, request.EquipmentIdHint),
                eventCollector,
                workflowToken);

            if (symptomResult.Error is not null)
            {
                await ReleaseReservationAsync(resolvedUserId, reservationId.Value);
                finalError = symptomResult.Error;
                return WorkflowResult.Failed(symptomResult.Error);
            }

            var diagnosticResult = await ExecuteStepAsync(
                diagnosticPlanner,
                new DiagnosticPlanInput(
                    symptomResult.Output.EquipmentId,
                    symptomResult.Output.ManualRevision,
                    symptomResult.Output.MatchedSymptoms),
                eventCollector,
                workflowToken);

            if (diagnosticResult.Error is not null)
            {
                await ReleaseReservationAsync(resolvedUserId, reservationId.Value);
                finalError = diagnosticResult.Error;
                return WorkflowResult.Failed(diagnosticResult.Error);
            }

            var workOrderResult = await ExecuteStepAsync(
                workOrderGenerator,
                new WorkOrderInput(symptomResult.Output.EquipmentId, diagnosticResult.Output),
                eventCollector,
                workflowToken);

            if (workOrderResult.Error is not null)
            {
                await ReleaseReservationAsync(resolvedUserId, reservationId.Value);
                finalStatus = AgentRunStatus.PartialSuccess;
                finalError = workOrderResult.Error;
                outputSummary = "Diagnostic plan produced; work order generation degraded.";
                return WorkflowResult.PartialSuccess(diagnosticResult.Output, workOrderResult.Error);
            }

            var llmCalls = collectedEvents.OfType<LlmCallCompleted>().ToArray();
            if (llmCalls.Length == 0)
            {
                throw new InvalidOperationException("The workflow completed without recorded LLM usage.");
            }

            var actualUsage = EquipFlow.Domain.Budget.ValueObjects.TokenUsage.FromActual(
                llmCalls.Sum(call => call.PromptTokens),
                llmCalls.Sum(call => call.CompletionTokens));
            var modelUsed = llmCalls
                .Select(call => call.ModelIdentifier)
                .FirstOrDefault(model => !string.IsNullOrWhiteSpace(model) && model != "unknown")
                ?? reservation.ModelName
                ?? "gpt-4o-mini";
            var reconciled = await costGovernor.ReconcileAsync(
                reservationId.Value.ToString(),
                actualUsage,
                modelUsed,
                workflowToken);
            if (!reconciled)
            {
                throw new InvalidOperationException("The workflow cost could not be reconciled.");
            }

            finalStatus = AgentRunStatus.Success;
            outputSummary = workOrderResult.Output.Summary;
            return WorkflowResult.PendingApproval(workOrderResult.Output);
        }
        catch (OperationCanceledException exception) when (workflowToken.IsCancellationRequested)
        {
            finalStatus = AgentRunStatus.Timeout;
            finalError = exception.Message;
            if (reservationId.HasValue)
            {
                await ReleaseReservationAsync(resolvedUserId, reservationId.Value);
            }

            throw;
        }
        catch
        {
            finalStatus = AgentRunStatus.Failed;
            if (reservationId.HasValue)
            {
                await ReleaseReservationAsync(resolvedUserId, reservationId.Value);
            }

            throw;
        }
        finally
        {
            collectedEvents.Add(new AgentRunCompleted(
                correlationId,
                DateTimeOffset.UtcNow,
                nameof(SequentialSupervisorOrchestrator),
                0,
                finalStatus,
                workflowStopwatch.ElapsedMilliseconds,
                finalError,
                outputSummary));

            await agentEventStore.AppendRangeAsync(collectedEvents, cancellationToken);
        }
    }

    private async Task<AgentResult<TOutput>> ExecuteStepAsync<TInput, TOutput>(
        IAgent<TInput, TOutput> agent,
        TInput input,
        IAgentContext context,
        CancellationToken workflowToken)
    {
        using var stepTimeout = CancellationTokenSource.CreateLinkedTokenSource(workflowToken);
        stepTimeout.CancelAfter(AgentTimeout);
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "AgentRunStarted {EventName} {CorrelationId} {AgentName}",
            "AgentRunStarted",
            context.CorrelationId,
            agent.Name);

        try
        {
            var result = await agent.ExecuteAsync(input, context, stepTimeout.Token);
            logger.LogInformation(
                "AgentRunCompleted {EventName} {CorrelationId} {AgentName} {Succeeded} {ElapsedMilliseconds}",
                "AgentRunCompleted",
                context.CorrelationId,
                agent.Name,
                result.Error is null,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (stepTimeout.IsCancellationRequested)
        {
            var timedOut = workflowToken.IsCancellationRequested
                ? "Agent execution stopped because the workflow timed out."
                : "Agent execution timed out after 45 seconds.";
            logger.LogWarning(
                "AgentRunCompleted {EventName} {CorrelationId} {AgentName} {Succeeded} {ElapsedMilliseconds} {Error}",
                "AgentRunCompleted",
                context.CorrelationId,
                agent.Name,
                false,
                stopwatch.ElapsedMilliseconds,
                timedOut);
            return new AgentResult<TOutput>(default!, [], false, timedOut);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "AgentRunCompleted {EventName} {CorrelationId} {AgentName} {Succeeded} {ElapsedMilliseconds}",
                "AgentRunCompleted",
                context.CorrelationId,
                agent.Name,
                false,
                stopwatch.ElapsedMilliseconds);
            return new AgentResult<TOutput>(default!, [], false, exception.Message);
        }
    }

    private Task ReleaseReservationAsync(string userId, Guid reservationId) =>
        costGovernor.ReleaseAsync(userId, reservationId, CancellationToken.None);

    private sealed class RecordingAgentContext(
        IAgentContext source,
        Guid correlationId,
        List<AgentEventBase> collectedEvents) : IAgentContext, IAgentEventCollector
    {
        public string CorrelationId => correlationId.ToString();

        public string UserId => source.UserId;

        public void Add(AgentEventBase @event) => collectedEvents.Add(@event);
    }
}

public record MaintenanceRequest(string UserId, string SymptomDescription, string? EquipmentIdHint = null);

public record WorkflowResult(
    WorkflowStatus Status,
    WorkOrderOutput? Draft,
    string? ErrorMessage,
    string? ReasonCode = null,
    decimal? EstimatedCost = null,
    decimal? RemainingBudget = null,
    string? CachedResponse = null,
    DiagnosticPlanOutput? DiagnosticPlan = null)
{
    public static WorkflowResult PendingApproval(WorkOrderOutput draft) =>
        new(WorkflowStatus.PendingApproval, draft, null);

    public static WorkflowResult Blocked(string reason) =>
        new(WorkflowStatus.Blocked, null, reason);

    public static WorkflowResult Blocked(
        string reason,
        decimal estimatedCost,
        decimal remainingBudget) =>
        new(WorkflowStatus.Blocked, null, reason, reason, estimatedCost, remainingBudget);

    public static WorkflowResult Cached(string response) =>
        new(WorkflowStatus.Cached, null, null, "semantic_cache_hit", CachedResponse: response);

    public static WorkflowResult Failed(string error) =>
        new(WorkflowStatus.Failed, null, error);

    public static WorkflowResult PartialSuccess(DiagnosticPlanOutput diagnosticPlan, string message) =>
        new(WorkflowStatus.PartialSuccess, null, message, DiagnosticPlan: diagnosticPlan);
}

public enum WorkflowStatus
{
    PendingApproval,
    Blocked,
    Failed,
    Cached,
    PartialSuccess
}