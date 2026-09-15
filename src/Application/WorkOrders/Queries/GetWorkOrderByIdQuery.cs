using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Queries;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId) : IRequest<WorkOrder?>;

public sealed class GetWorkOrderByIdQueryHandler(IWorkOrderRepository repository)
    : IRequestHandler<GetWorkOrderByIdQuery, WorkOrder?>
{
    public Task<WorkOrder?> Handle(
        GetWorkOrderByIdQuery request,
        CancellationToken cancellationToken) =>
        repository.GetByIdAsync(request.WorkOrderId, cancellationToken);
}