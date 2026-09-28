using EquipFlow.Application.Conversations.Commands;
using EquipFlow.Application.Conversations.Ports;
using EquipFlow.Domain.Entities;
using MediatR;

namespace EquipFlow.Application.Conversations.Handlers;

public sealed class StartConversationCommandHandler(IConversationRepository repository)
    : IRequestHandler<StartConversationCommand, Guid>
{
    public async Task<Guid> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = new Conversation(request.UserId, request.Title);
        await repository.AddAsync(conversation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return conversation.Id;
    }
}
