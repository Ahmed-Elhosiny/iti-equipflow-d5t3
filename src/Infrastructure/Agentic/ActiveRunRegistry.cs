using System.Collections.Concurrent;
using EquipFlow.Application.Agentic.Abstractions;

namespace EquipFlow.Infrastructure.Agentic;

public sealed class ActiveRunRegistry : IActiveRunRegistry
{
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource Cts, string UserId)> _activeRuns = new();

    public void Register(Guid runId, string userId, CancellationTokenSource cts)
    {
        _activeRuns.TryAdd(runId, (cts, userId));
    }

    public void Unregister(Guid runId)
    {
        _activeRuns.TryRemove(runId, out _);
    }

    public bool TryCancel(Guid runId, string userId)
    {
        if (_activeRuns.TryGetValue(runId, out var entry))
        {
            // Enforce object-level ownership: only the user who started the run can cancel it
            if (string.Equals(entry.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                if (!entry.Cts.IsCancellationRequested)
                {
                    entry.Cts.Cancel();
                    return true;
                }
            }
            // Return false if user doesn't own it to prevent enumeration (acts as NotFound)
        }
        return false;
    }
}