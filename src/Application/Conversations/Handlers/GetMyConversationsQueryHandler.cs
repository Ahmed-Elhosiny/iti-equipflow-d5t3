using EquipFlow.Application.Conversations.Queries;
using EquipFlow.Application.Conversations.Queries.Dtos;
using EquipFlow.Application.Conversations.Ports;
using MediatR;

namespace EquipFlow.Application.Conversations.Handlers;

public sealed class GetMyConversationsQueryHandler(IConversationRepository repository)
    : IRequestHandler<GetMyConversationsQuery, IReadOnlyList<ConversationSummaryDto>>
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> Handle(GetMyConversationsQuery request, CancellationToken cancellationToken)
    {
        var conversations = await repository.GetByUserIdAsync(request.UserId, cancellationToken);
        
        return conversations.Select(c => new ConversationSummaryDto(
            c.Id,
            c.Title,
            c.UpdatedAtUtc,
            c.Messages.Count)).ToList();
    }
}
