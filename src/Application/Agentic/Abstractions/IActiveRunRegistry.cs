namespace EquipFlow.Application.Agentic.Abstractions;

public interface IActiveRunRegistry
{
    void Register(Guid runId, CancellationTokenSource cts);
    void Unregister(Guid runId);
    bool TryCancel(Guid runId);
}