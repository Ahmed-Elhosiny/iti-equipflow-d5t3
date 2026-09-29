using EquipFlow.Application.Agentic.Events;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface IAgent<TInput, TOutput>
{
    string Name { get; }

    Task<AgentResult<TOutput>> ExecuteAsync(
        TInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}

public interface IAgentContext
{
    string CorrelationId { get; }
    string UserId { get; }
    string UserRole { get; } 
    string? ReservationId { get; }
}

public interface IAgentEventCollector
{
    void Add(AgentEventBase @event);
}

public sealed record AgentResult<T>(
    T Output,
    IReadOnlyList<Citation> Citations,
    bool IsGrounded = true,
    string? Error = null);

public sealed record Citation(
    string DocumentId,
    string ChunkId,
    string? Section,
    int? Page,
    float Score);