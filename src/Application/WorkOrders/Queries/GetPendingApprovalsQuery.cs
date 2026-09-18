using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Enums;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Queries;

public sealed record GetPendingApprovalsQuery : IRequest<IEnumerable<WorkOrderDto>>;

public sealed class GetPendingApprovalsQueryHandler(IWorkOrderRepository repository)
    : IRequestHandler<GetPendingApprovalsQuery, IEnumerable<WorkOrderDto>>
{
    public async Task<IEnumerable<WorkOrderDto>> Handle(
        GetPendingApprovalsQuery request,
        CancellationToken cancellationToken)
    {
        var workOrders = await repository.GetByStatusAsync(
            WorkOrderStatus.PendingApproval,
            cancellationToken);

        return workOrders.Select(WorkOrderDto.FromDomain);
    }
}