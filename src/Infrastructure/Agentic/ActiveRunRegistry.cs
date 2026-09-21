using System.Collections.Concurrent;
using EquipFlow.Application.Agentic.Abstractions;

namespace EquipFlow.Infrastructure.Agentic;

public sealed class ActiveRunRegistry : IActiveRunRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();

    public void Register(Guid runId, CancellationTokenSource cts)
    {
        _activeRuns.TryAdd(runId, cts);
    }

    public void Unregister(Guid runId)
    {
        _activeRuns.TryRemove(runId, out _);
    }

    public bool TryCancel(Guid runId)
    {
        if (_activeRuns.TryGetValue(runId, out var cts))
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
                return true;
            }
        }
        return false;
    }
}