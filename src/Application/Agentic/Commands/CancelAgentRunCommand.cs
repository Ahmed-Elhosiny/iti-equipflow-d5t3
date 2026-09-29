using MediatR;

namespace EquipFlow.Application.Agentic.Commands;

public sealed record CancelAgentRunCommand(Guid RunId, string UserId) : IRequest<bool>;