using EquipFlow.Application.Conversations.Queries;
using EquipFlow.Application.Conversations.Queries.Dtos;
using EquipFlow.Application.Conversations.Ports;
using MediatR;

namespace EquipFlow.Application.Conversations.Handlers;

public sealed class GetConversationByIdQueryHandler(IConversationRepository repository)
    : IRequestHandler<GetConversationByIdQuery, ConversationDto>
{
    public async Task<ConversationDto> Handle(GetConversationByIdQuery request, CancellationToken cancellationToken)
    {
        var conversation = await repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {request.ConversationId} not found.");

        // Object-level authorization: Fail closed to prevent resource enumeration (IDOR)
        if (!string.Equals(conversation.UserId, request.RequestingUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Conversation {request.ConversationId} not found.");
        }

        return new ConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.UserId,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            conversation.Messages.Select(m => new ConversationMessageDto(
                m.Id, m.Role, m.Content, m.Metadata, m.CreatedAtUtc)).ToList());
    }
}
