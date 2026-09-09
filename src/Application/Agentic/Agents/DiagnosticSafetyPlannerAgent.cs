using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class DiagnosticSafetyPlannerAgent : IAgent<DiagnosticPlanInput, DiagnosticPlanOutput>
{
    public string Name => "DiagnosticSafetyPlanner";

    public Task<AgentResult<DiagnosticPlanOutput>> ExecuteAsync(
        DiagnosticPlanInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Skeleton: Implement diagnostic steps and safety prerequisite extraction.");
}