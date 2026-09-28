using MediatR;

namespace EquipFlow.Application.Conversations.Commands;

public sealed record StartConversationCommand(
    string UserId,
    string Title) : IRequest<Guid>;
