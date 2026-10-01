using System.Diagnostics;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Ports;
using EquipFlow.Application.Search.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Application.Agentic.Orchestration;

public sealed class SequentialSupervisorOrchestrator(
    IAgent<SymptomMatchInput, SymptomMatchOutput> symptomMatcher,
    IAgent<DiagnosticPlanInput, DiagnosticPlanOutput> diagnosticPlanner,
    IAgent<WorkOrderInput, WorkOrderOutput> workOrderGenerator,
    ICostGovernor costGovernor,
    IAgentEventStore agentEventStore,
    ISender sender,
    ITokenEstimator tokenEstimator,
    Microsoft.Extensions.Options.IOptions<EquipFlow.Application.Options.OpenAIOptions> openAiOptions, 
    ILogger<SequentialSupervisorOrchestrator> logger,
    ICachePort? cachePort = null)
{
    private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan WorkflowTimeout = TimeSpan.FromSeconds(120);
    private readonly Microsoft.Extensions.Options.IOptions<EquipFlow.Application.Options.OpenAIOptions> _openAiOptions = openAiOptions;

    public async Task<WorkflowResult> RunWorkflowAsync(
        MaintenanceRequest request,
        IAgentContext context,
        Action<AgentEventBase>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.TryParse(context.CorrelationId, out var parsedCorrelationId)
            ? parsedCorrelationId
            : Guid.NewGuid();
        var collectedEvents = new List<AgentEventBase>();
        var workflowStopwatch = Stopwatch.StartNew();
        var eventCollector = new RecordingAgentContext(context, correlationId, collectedEvents, onEvent);
        var finalStatus = AgentRunStatus.Failed;
        string? finalError = null;
        string? outputSummary = null;

        // Resolve the user ID from the context or request, throwing an exception if neither is available.
        var resolvedUserId = !string.IsNullOrWhiteSpace(context.UserId)
            ? context.UserId
            : !string.IsNullOrWhiteSpace(request.UserId)
                ? request.UserId
                : throw new InvalidOperationException("User identity is required. Anonymous execution is blocked by the Cost Governor.");

        collectedEvents.Add(new AgentRunStarted(
            correlationId,
            DateTimeOffset.UtcNow,
            nameof(SequentialSupervisorOrchestrator),
            0,
            request.SymptomDescription,
            (int)WorkflowTimeout.TotalMilliseconds,
            resolvedUserId)); 
            
        onEvent?.Invoke(collectedEvents.Last());

        using var workflowTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workflowTimeout.CancelAfter(WorkflowTimeout);
        var workflowToken = workflowTimeout.Token;

        try
        {
            // --- STEP 1: Symptom Matcher ---
            var (symptomResult, step1Gov) = await ExecuteStepWithBudgetAsync(
                symptomMatcher,
                new SymptomMatchInput(request.SymptomDescription, request.EquipmentIdHint),
                request.SymptomDescription,
                resolvedUserId,
                correlationId.ToString(),
                eventCollector,
                workflowToken,
                semanticQuery: request.SymptomDescription);

            if (step1Gov?.Status == CostGovernorStatus.Cached)
            {
                finalStatus = AgentRunStatus.Success;
                outputSummary = "Semantic cache hit.";
                return WorkflowResult.Cached(step1Gov.CachedResponse!);
            }

            if (symptomResult.Error is not null)
            {
                var fallbackResult = await TryRagFallbackAsync(request.SymptomDescription, symptomResult.Error, workflowToken);
                if (fallbackResult is not null)
                {
                    finalStatus = AgentRunStatus.PartialSuccess;
                    outputSummary = "Degraded to RAG fallback due to agent failure.";
                    return fallbackResult;
                }
                finalError = symptomResult.Error;
                return WorkflowResult.Failed(symptomResult.Error);
            }

            // --- STEP 2: Diagnostic Planner ---
            var step2InputText = $"{symptomResult.Output.EquipmentId} {string.Join(" ", symptomResult.Output.MatchedSymptoms)}";
            var (diagnosticResult, _) = await ExecuteStepWithBudgetAsync(
                diagnosticPlanner,
                new DiagnosticPlanInput(
                    symptomResult.Output.EquipmentId,
                    symptomResult.Output.ManualRevision,
                    symptomResult.Output.MatchedSymptoms),
                step2InputText,
                resolvedUserId,
                correlationId.ToString(),
                eventCollector,
                workflowToken);

            if (diagnosticResult.Error is not null)
            {
                var fallbackResult = await TryRagFallbackAsync(request.SymptomDescription, diagnosticResult.Error, workflowToken);
                if (fallbackResult is not null)
                {
                    finalStatus = AgentRunStatus.PartialSuccess;
                    outputSummary = "Degraded to RAG fallback due to agent failure.";
                    return fallbackResult;
                }
                finalError = diagnosticResult.Error;
                return WorkflowResult.Failed(diagnosticResult.Error);
            }

            // --- STEP 3: Work Order Generator ---
            var step3InputText = $"{symptomResult.Output.EquipmentId} {diagnosticResult.Output.Reasoning}";
            var (workOrderResult, _) = await ExecuteStepWithBudgetAsync(
                workOrderGenerator,
                new WorkOrderInput(symptomResult.Output.EquipmentId, diagnosticResult.Output),
                step3InputText,
                resolvedUserId,
                correlationId.ToString(),
                eventCollector,
                workflowToken);

            if (workOrderResult.Error is not null)
            {
                finalStatus = AgentRunStatus.PartialSuccess;
                finalError = workOrderResult.Error;
                outputSummary = "Diagnostic plan produced; work order generation degraded.";
                return WorkflowResult.PartialSuccess(diagnosticResult.Output, workOrderResult.Error);
            }

            finalStatus = AgentRunStatus.Success;
            outputSummary = workOrderResult.Output.Summary;

            // Write to semantic cache for future zero-cost fallbacks
            if (cachePort is not null && !string.IsNullOrWhiteSpace(outputSummary))
            {
                try
                {
                    await cachePort.AddAsync(request.SymptomDescription, outputSummary, workflowToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to write successful workflow result to semantic cache.");
                }
            }

            return WorkflowResult.PendingApproval(workOrderResult.Output);
        }
        catch (OperationCanceledException exception) when (workflowToken.IsCancellationRequested)
        {
            finalStatus = AgentRunStatus.Timeout;
            finalError = exception.Message;
            throw;
        }
        catch
        {
            finalStatus = AgentRunStatus.Failed;
            throw;
        }
        finally
        {
            var completedEvent = new AgentRunCompleted(
                correlationId,
                DateTimeOffset.UtcNow,
                nameof(SequentialSupervisorOrchestrator),
                0,
                finalStatus,
                workflowStopwatch.ElapsedMilliseconds,
                finalError,
                outputSummary);
                
            collectedEvents.Add(completedEvent);
            onEvent?.Invoke(completedEvent);

            await agentEventStore.AppendRangeAsync(collectedEvents, CancellationToken.None);
        }
    }

       private async Task<(AgentResult<TOutput> Result, CostGovernorResult? GovResult)> ExecuteStepWithBudgetAsync<TInput, TOutput>(
        IAgent<TInput, TOutput> agent,
        TInput input,
        string stepInputText,
        string userId,
        string correlationId,
        RecordingAgentContext eventCollector,
        CancellationToken workflowToken,
        string? semanticQuery = null)
    {
        var estimatedTokens = tokenEstimator.EstimateTokens(stepInputText);
        // const decimal preFlightPricePer1KTokens = 0.01m;

        var reservation = await costGovernor.EstimateAndReserveAsync(
            userId,
            estimatedTokens,
            _openAiOptions.Value.Model,
            workflowToken,
            semanticQuery);

        if (reservation.Status == CostGovernorStatus.Blocked || reservation.Status == CostGovernorStatus.Cached)
        {
            var blockedOrCachedResult = new AgentResult<TOutput>(default!, [], false, reservation.Status == CostGovernorStatus.Blocked ? reservation.Reason : "CACHE_HIT");
            return (blockedOrCachedResult, reservation);
        }

        // Inject the active budget reservation ID into the agent context so downstream
        // LLM calls and tool executions can prove they are operating within an approved budget envelope (ADR-004).
        eventCollector.ReservationId = reservation.ReservationId?.ToString();

        var eventsBefore = eventCollector.CollectedEvents.OfType<LlmCallCompleted>().Count();
        var agentResult = await ExecuteStepAsync(agent, input, eventCollector, workflowToken);

        var stepLlmCalls = eventCollector.CollectedEvents.OfType<LlmCallCompleted>().Skip(eventsBefore).ToArray();
        
        if (stepLlmCalls.Length > 0)
        {
            var actualUsage = EquipFlow.Domain.Budget.ValueObjects.TokenUsage.FromActual(
                stepLlmCalls.Sum(c => c.PromptTokens),
                stepLlmCalls.Sum(c => c.CompletionTokens));
            var modelUsed = stepLlmCalls
                .Select(c => c.ModelIdentifier)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m) && m != "unknown")
                ?? reservation.ModelName ?? "gpt-4o-mini";

            await costGovernor.ReconcileAsync(
                userId,
                reservation.ReservationId!.Value.ToString(),
                actualUsage,
                modelUsed,
                reservation.ReservationId!.Value.ToString(), // Use ReservationId as the unique RunId for spend tracking
                workflowToken);
        }
        else
        {
            // If no LLM calls happened (e.g., agent failed before calling LLM), release the reservation
            await costGovernor.ReleaseAsync(userId, reservation.ReservationId!.Value, workflowToken);
        }

        return (agentResult, reservation);
    }

    private async Task<WorkflowResult?> TryRagFallbackAsync(string symptomDescription, string originalError, CancellationToken cancellationToken)
    {
        try
        {
            var searchResult = await sender.Send(new SearchDocumentsQuery(symptomDescription, TopK: 5), cancellationToken);
            
            if (searchResult.IsRefusal || searchResult.Results is null || searchResult.Results.Count == 0)
            {
                logger.LogWarning("RAG fallback failed or found no relevant documents: {Reason}", searchResult.RefusalReason);
                return null;
            }

            return WorkflowResult.Fallback(searchResult.Results, originalError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG fallback threw an exception.");
            return null;
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

    private sealed class RecordingAgentContext(
        IAgentContext source,
        Guid correlationId,
        List<AgentEventBase> collectedEvents,
        Action<AgentEventBase>? onEvent) : IAgentContext, IAgentEventCollector
    {
        public List<AgentEventBase> CollectedEvents => collectedEvents;

        public string CorrelationId => correlationId.ToString();

        public string UserId => source.UserId;
        public string UserRole => source.UserRole;
        public string? ReservationId { get; set; }

        public void Add(AgentEventBase @event) 
        {
            collectedEvents.Add(@event);
            onEvent?.Invoke(@event);
        }
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
    DiagnosticPlanOutput? DiagnosticPlan = null,
    IReadOnlyList<EquipFlow.Domain.Search.SearchResult>? FallbackSearchResults = null)
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

    public static WorkflowResult Fallback(IReadOnlyList<EquipFlow.Domain.Search.SearchResult> results, string originalError) =>
        new(WorkflowStatus.Fallback, null, originalError, "agent_failure_fallback_rag", FallbackSearchResults: results);
}

public enum WorkflowStatus
{
    PendingApproval,
    Blocked,
    Failed,
    Cached,
    PartialSuccess,
    Fallback
}