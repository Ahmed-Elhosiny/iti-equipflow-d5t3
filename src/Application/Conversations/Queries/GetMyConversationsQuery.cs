using EquipFlow.Application.Conversations.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Conversations.Queries;

public sealed record GetMyConversationsQuery(
    string UserId) : IRequest<IReadOnlyList<ConversationSummaryDto>>;
