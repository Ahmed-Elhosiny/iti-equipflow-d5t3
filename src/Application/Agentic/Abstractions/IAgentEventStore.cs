using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EquipFlow.Application.Agentic.Events;

namespace EquipFlow.Application.Agentic.Abstractions;

/// <summary>
/// Application-layer port for persisting and querying agent observability events.
/// Supports the tracing requirements in OBS-002 and the cost governance
/// reconciliation and attribution requirements in CG-006 and CG-007.
/// </summary>
public interface IAgentEventStore
{
    /// <summary>
    /// Persists a single agent observability event.
    /// </summary>
    /// <param name="event">The event to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AppendAsync(AgentEventBase @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of agent observability events efficiently, such as the
    /// complete trace captured at the end of an agent run.
    /// </summary>
    /// <param name="events">The events to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AppendRangeAsync(
        IEnumerable<AgentEventBase> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events for an agent run in chronological order.
    /// </summary>
    /// <param name="correlationId">The identifier shared by the agent run's events.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<AgentEventBase>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default);
}