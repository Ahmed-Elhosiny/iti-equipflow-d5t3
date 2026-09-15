using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core adapter for persisting and retrieving agent observability events.
/// </summary>
public sealed class AgentEventStore : IAgentEventStore
{
    private readonly EquipFlowDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEventStore"/> class.
    /// </summary>
    /// <param name="dbContext">The database context used to persist events.</param>
    public AgentEventStore(EquipFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AppendAsync(
        AgentEventBase @event,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AgentEvents.AddRangeAsync(
            new[] { ToEntity(@event) },
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(
        IEnumerable<AgentEventBase> events,
        CancellationToken cancellationToken = default)
    {
        var entities = events.Select(ToEntity).ToList();

        await _dbContext.AgentEvents.AddRangeAsync(entities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentEventBase>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AgentEvents
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(ToEvent).ToList();
    }

    private static AgentEventEntity ToEntity(AgentEventBase @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new AgentEventEntity
        {
            CorrelationId = @event.CorrelationId,
            Timestamp = @event.Timestamp,
            AgentName = @event.AgentName,
            StepIndex = @event.StepIndex,
            EventType = @event.GetType().Name,
            Payload = JsonSerializer.Serialize(@event, @event.GetType())
        };
    }

    private static AgentEventBase ToEvent(AgentEventEntity entity)
    {
        AgentEventBase? eventType = entity.EventType switch
        {
            nameof(AgentRunStarted) => JsonSerializer.Deserialize<AgentRunStarted>(entity.Payload),
            nameof(AgentRunCompleted) => JsonSerializer.Deserialize<AgentRunCompleted>(entity.Payload),
            nameof(LlmCallCompleted) => JsonSerializer.Deserialize<LlmCallCompleted>(entity.Payload),
            nameof(ToolInvoked) => JsonSerializer.Deserialize<ToolInvoked>(entity.Payload),
            nameof(CitationAttached) => JsonSerializer.Deserialize<CitationAttached>(entity.Payload),
            _ => throw new InvalidOperationException(
                $"Unsupported agent event type '{entity.EventType}'.")
        };

        return eventType
            ?? throw new InvalidOperationException(
                $"The payload for agent event '{entity.Id}' could not be deserialized.");
    }
}