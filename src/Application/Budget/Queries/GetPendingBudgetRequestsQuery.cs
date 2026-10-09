using MediatR;

namespace EquipFlow.Application.Budget.Queries;

public sealed record GetPendingBudgetRequestsQuery : IRequest<IReadOnlyList<PendingBudgetRequestDto>>;

public sealed record PendingBudgetRequestDto(
    Guid Id,
    Guid UserId,
    decimal RequestedAmount,
    string Reason,
    DateTimeOffset CreatedAt);
