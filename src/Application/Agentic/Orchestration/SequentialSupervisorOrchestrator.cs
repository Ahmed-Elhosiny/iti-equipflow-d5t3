using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;

namespace EquipFlow.Application.Agentic.Orchestration;

public sealed class SequentialSupervisorOrchestrator(
    IAgent<SymptomMatchInput, SymptomMatchOutput> symptomMatcher,
    IAgent<DiagnosticPlanInput, DiagnosticPlanOutput> diagnosticPlanner,
    IAgent<WorkOrderInput, WorkOrderOutput> workOrderGenerator,
    ICostGovernor costGovernor)
{
    public async Task<WorkflowResult> RunWorkflowAsync(
        MaintenanceRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var budget = await costGovernor.CheckBudgetAsync(
            request.UserId,
            new EstimatedCost(0.05m, 100, 100),
            cancellationToken);

        if (!budget.IsAllowed)
        {
            return WorkflowResult.Blocked(budget.Reason ?? "Cost governor blocked the workflow.");
        }

        var symptomResult = await symptomMatcher.ExecuteAsync(
            new SymptomMatchInput(request.SymptomDescription, request.EquipmentIdHint),
            context,
            cancellationToken);

        if (symptomResult.Error is not null)
        {
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
            return WorkflowResult.Failed(diagnosticResult.Error);
        }

        var workOrderResult = await workOrderGenerator.ExecuteAsync(
            new WorkOrderInput(symptomResult.Output.EquipmentId, diagnosticResult.Output),
            context,
            cancellationToken);

        if (workOrderResult.Error is not null)
        {
            return WorkflowResult.Failed(workOrderResult.Error);
        }

        return WorkflowResult.PendingApproval(workOrderResult.Output);
    }
}

public record MaintenanceRequest(string UserId, string SymptomDescription, string? EquipmentIdHint = null);

public record WorkflowResult(WorkflowStatus Status, WorkOrderOutput? Draft, string? ErrorMessage)
{
    public static WorkflowResult PendingApproval(WorkOrderOutput draft) =>
        new(WorkflowStatus.PendingApproval, draft, null);

    public static WorkflowResult Blocked(string reason) =>
        new(WorkflowStatus.Blocked, null, reason);

    public static WorkflowResult Failed(string error) =>
        new(WorkflowStatus.Failed, null, error);
}

public enum WorkflowStatus
{
    PendingApproval,
    Blocked,
    Failed
}