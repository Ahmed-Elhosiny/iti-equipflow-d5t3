using System.Text.Json;
using MediatR;

namespace EquipFlow.Application.Agentic.Queries;

public sealed record GetAgentRunByIdQuery(Guid CorrelationId, string RequestingUserId) : IRequest<AgentRunDto?>;

public sealed record AgentRunDto(
    Guid CorrelationId,
    string? Status,
    DateTime StartedAt,
    DateTime? EndedAt,
    List<AgentEventDto> Events,
    decimal TotalCostUsd);

public sealed record AgentEventDto(
    string EventType,
    DateTime Timestamp,
    string AgentName,
    JsonElement Payload,
    bool? Success);