using EquipFlow.Application.Conversations.Queries.Dtos;
using MediatR;

namespace EquipFlow.Application.Conversations.Queries;

public sealed record GetConversationByIdQuery(
    Guid ConversationId,
    string RequestingUserId) : IRequest<ConversationDto>;
