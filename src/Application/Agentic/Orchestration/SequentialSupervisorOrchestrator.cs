using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;

namespace EquipFlow.Application.Agentic.Orchestration;

public sealed class SequentialSupervisorOrchestrator(
    IAgent<SymptomMatchInput, SymptomMatchOutput> symptomMatcher,
    IAgent<DiagnosticPlanInput, DiagnosticPlanOutput> diagnosticPlanner,
    IAgent<WorkOrderInput, WorkOrderOutput> workOrderGenerator,
    ICostGovernor costGovernor)
{
    private const string MockUserId = "00000000-0000-0000-0000-000000000001";

    public async Task<WorkflowResult> RunWorkflowAsync(
        MaintenanceRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        // TODO: Extract the user identity from the authenticated user context.
        var userId = string.IsNullOrWhiteSpace(context.UserId)
            ? string.IsNullOrWhiteSpace(request.UserId) ? MockUserId : request.UserId
            : context.UserId;

        var reservation = await costGovernor.EstimateAndReserveAsync(
            userId,
            estimatedTokens: 3000,
            pricePerThousandTokens: 0.01m,
            cancellationToken);

        if (reservation.Status == CostGovernorStatus.Blocked)
        {
            return WorkflowResult.Blocked(
                reservation.Reason,
                reservation.EstimatedCost,
                reservation.RemainingBudget);
        }

        if (reservation.Status == CostGovernorStatus.Cached)
        {
            return WorkflowResult.Cached(reservation.CachedResponse!);
        }

        var reservationId = reservation.ReservationId!.Value;

        try
        {
            var symptomResult = await symptomMatcher.ExecuteAsync(
                new SymptomMatchInput(request.SymptomDescription, request.EquipmentIdHint),
                context,
                cancellationToken);

            if (symptomResult.Error is not null)
            {
                await costGovernor.ReleaseAsync(userId, reservationId, cancellationToken);
                return WorkflowResult.Failed(symptomResult.Error);
            }

            var diagnosticResult = await diagnosticPlanner.ExecuteAsync(
                new DiagnosticPlanInput(
                    symptomResult.Output.EquipmentId,
                    symptomResult.Output.ManualRevision,
                    symptomResult.Output.MatchedSymptoms),
                context,
                cancellationToken);

            if (diagnosticResult.Error is not null)
            {
                await costGovernor.ReleaseAsync(userId, reservationId, cancellationToken);
                return WorkflowResult.Failed(diagnosticResult.Error);
            }

            var workOrderResult = await workOrderGenerator.ExecuteAsync(
                new WorkOrderInput(symptomResult.Output.EquipmentId, diagnosticResult.Output),
                context,
                cancellationToken);

            if (workOrderResult.Error is not null)
            {
                await costGovernor.ReleaseAsync(userId, reservationId, cancellationToken);
                return WorkflowResult.Failed(workOrderResult.Error);
            }

            await costGovernor.CommitAsync(userId, reservationId, actualUsageCost: 0.025m, cancellationToken);
            return WorkflowResult.PendingApproval(workOrderResult.Output);
        }
        catch
        {
            await costGovernor.ReleaseAsync(userId, reservationId, cancellationToken);
            throw;
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
    string? CachedResponse = null)
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
}

public enum WorkflowStatus
{
    PendingApproval,
    Blocked,
    Failed,
    Cached
}