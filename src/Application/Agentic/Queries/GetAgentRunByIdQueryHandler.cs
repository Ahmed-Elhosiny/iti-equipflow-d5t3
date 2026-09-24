using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Events;
using MediatR;

namespace EquipFlow.Application.Agentic.Queries;

public sealed class GetAgentRunByIdQueryHandler(IAgentEventStore eventStore)
    : IRequestHandler<GetAgentRunByIdQuery, AgentRunDto?>
{
    public async Task<AgentRunDto?> Handle(
        GetAgentRunByIdQuery request,
        CancellationToken cancellationToken)
    {
        var events = (await eventStore.GetByCorrelationIdAsync(
            request.CorrelationId,
            cancellationToken)).ToList();

        if (events.Count == 0)
        {
            return null;
        }

        var startedEvent = events.OfType<AgentRunStarted>().FirstOrDefault();
        
        // Object-level authorization: Fail closed with 404 to prevent enumeration
        if (startedEvent is null || !string.Equals(startedEvent.UserId, request.RequestingUserId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var completedEvent = events.OfType<AgentRunCompleted>().FirstOrDefault();

        return new AgentRunDto(
            request.CorrelationId,
            completedEvent?.Status.ToString(),
            (startedEvent ?? events[0]).Timestamp.UtcDateTime,
            completedEvent?.Timestamp.UtcDateTime,
            events.Select(ToDto).ToList());
    }

    private static AgentEventDto ToDto(AgentEventBase @event)
    {
        var success = @event switch
        {
            AgentRunCompleted completed => (bool?)(completed.Status == AgentRunStatus.Success),
            ToolInvoked tool => (bool?)(tool.Status == ToolInvocationStatus.Success),
            _ => (bool?)null
        };

        return new AgentEventDto(
            @event.GetType().Name,
            @event.Timestamp.UtcDateTime,
            @event.AgentName,
            JsonSerializer.Serialize(@event, @event.GetType()),
            success);
    }
}