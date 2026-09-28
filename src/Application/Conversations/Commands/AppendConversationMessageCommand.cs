using MediatR;

namespace EquipFlow.Application.Conversations.Commands;

public sealed record AppendConversationMessageCommand(
    Guid ConversationId,
    string RequestingUserId,
    string Role,
    string Content,
    string? Metadata = null) : IRequest<Guid>;
