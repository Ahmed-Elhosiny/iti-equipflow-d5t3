using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class SymptomMatcherAgent : IAgent<SymptomMatchInput, SymptomMatchOutput>
{
    public string Name => "SymptomMatcher";

    public Task<AgentResult<SymptomMatchOutput>> ExecuteAsync(
        SymptomMatchInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Skeleton: Implement RAG and symptom matching logic.");
}