namespace EquipFlow.Application.Agentic.Abstractions;

public interface IActiveRunRegistry
{
    void Register(Guid runId, string userId, CancellationTokenSource cts);
    void Unregister(Guid runId);
    bool TryCancel(Guid runId, string userId);
}