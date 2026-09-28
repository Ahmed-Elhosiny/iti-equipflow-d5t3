using EquipFlow.Application.Conversations.Commands;
using EquipFlow.Application.Conversations.Ports;
using MediatR;

namespace EquipFlow.Application.Conversations.Handlers;

public sealed class AppendConversationMessageCommandHandler(IConversationRepository repository)
    : IRequestHandler<AppendConversationMessageCommand, Guid>
{
    public async Task<Guid> Handle(AppendConversationMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {request.ConversationId} not found.");

        // Object-level authorization: Ensure the user owns the conversation
        if (!string.Equals(conversation.UserId, request.RequestingUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Conversation {request.ConversationId} not found.");
        }

        var message = conversation.AddMessage(request.Role, request.Content, request.Metadata);
        await repository.SaveChangesAsync(cancellationToken);
        return message.Id;
    }
}
