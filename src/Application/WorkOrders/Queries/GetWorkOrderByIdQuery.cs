using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Application.Common;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using MediatR;

namespace EquipFlow.Application.WorkOrders.Queries;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId) : IRequest<WorkOrderDto>;

public sealed class GetWorkOrderByIdQueryHandler(IWorkOrderRepository repository)
    : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(
        GetWorkOrderByIdQuery request,
        CancellationToken cancellationToken) =>
        WorkOrderDto.FromDomain(await repository.GetByIdAsync(request.WorkOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(request.WorkOrderId));
}

public sealed record WorkOrderDto(
    Guid Id,
    string Title,
    string Symptom,
    string EquipmentName,
    string? EquipmentAssetNumber,
    string? ManualRevision,
    string? Location,
    WorkOrderStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? DecisionBy,
    DateTimeOffset? DecisionAtUtc,
    string? DecisionComment)
{
    public static WorkOrderDto FromDomain(WorkOrder workOrder) => new(
        workOrder.Id,
        workOrder.Title,
        workOrder.Symptom,
        workOrder.EquipmentName,
        workOrder.EquipmentAssetNumber,
        workOrder.ManualRevision,
        workOrder.Location,
        workOrder.Status,
        workOrder.CreatedBy,
        workOrder.CreatedAtUtc,
        workOrder.UpdatedAtUtc,
        workOrder.DecisionBy,
        workOrder.DecisionAtUtc,
        workOrder.DecisionComment);
}