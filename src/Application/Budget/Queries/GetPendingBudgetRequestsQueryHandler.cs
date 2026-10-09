using EquipFlow.Application.Ports;
using MediatR;

namespace EquipFlow.Application.Budget.Queries;

public sealed class GetPendingBudgetRequestsQueryHandler(IBudgetIncreaseRequestRepository repository)
    : IRequestHandler<GetPendingBudgetRequestsQuery, IReadOnlyList<PendingBudgetRequestDto>>
{
    public async Task<IReadOnlyList<PendingBudgetRequestDto>> Handle(
        GetPendingBudgetRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var pendingRequests = await repository.GetPendingAsync(cancellationToken);
        
        return pendingRequests
            .Select(r => new PendingBudgetRequestDto(
                r.Id,
                r.UserId,
                r.RequestedAmount,
                r.Reason,
                r.CreatedAt))
            .ToList();
    }
}
