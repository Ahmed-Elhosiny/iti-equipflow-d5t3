using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Enums;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Queries;

public sealed record GetWorkOrdersByUserIdQuery(
    Guid UserId,
    WorkOrderStatus? StatusFilter = null) : IRequest<IEnumerable<WorkOrderDto>>;

public sealed class GetWorkOrdersByUserIdQueryHandler(IWorkOrderRepository repository)
    : IRequestHandler<GetWorkOrdersByUserIdQuery, IEnumerable<WorkOrderDto>>
{
    public async Task<IEnumerable<WorkOrderDto>> Handle(
        GetWorkOrdersByUserIdQuery request,
        CancellationToken cancellationToken)
    {
        var workOrders = await repository.GetByUserIdAsync(
            request.UserId,
            request.StatusFilter,
            cancellationToken);

        return workOrders.Select(WorkOrderDto.FromDomain);
    }
}