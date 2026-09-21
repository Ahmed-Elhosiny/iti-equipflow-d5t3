using EquipFlow.Application.Equipment.Queries.Dtos;
using EquipFlow.Application.Ports;
using EquipFlow.Application.WorkOrders.Ports;
using MediatR;

namespace EquipFlow.Application.Equipment.Queries;

public sealed class GetEquipmentMaintenanceQueryHandler(
    IEquipmentRepository equipmentRepository,
    IWorkOrderRepository workOrderRepository)
    : IRequestHandler<GetEquipmentMaintenanceQuery, IReadOnlyList<EquipmentMaintenanceDto>>
{
    public async Task<IReadOnlyList<EquipmentMaintenanceDto>> Handle(
        GetEquipmentMaintenanceQuery request,
        CancellationToken cancellationToken)
    {
        var equipment = await equipmentRepository.GetByIdAsync(request.EquipmentId, cancellationToken);
        
        if (equipment is null)
        {
            throw new KeyNotFoundException($"Equipment with ID {request.EquipmentId} not found.");
        }

        // WorkOrder stores EquipmentName, so we query by the resolved equipment name
        var workOrders = await workOrderRepository.GetByEquipmentNameAsync(equipment.Name, cancellationToken);

        return workOrders.Select(wo => new EquipmentMaintenanceDto(
            wo.Id,
            wo.Title,
            wo.Symptom,
            wo.Status.ToString(),
            wo.CreatedAtUtc,
            wo.DecisionComment
        )).ToList();
    }
}