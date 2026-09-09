using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class WorkOrderGeneratorAgent : IAgent<WorkOrderInput, WorkOrderOutput>
{
    public string Name => "WorkOrderGenerator";

    public Task<AgentResult<WorkOrderOutput>> ExecuteAsync(
        WorkOrderInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Skeleton: Implement work order drafting logic.");
}