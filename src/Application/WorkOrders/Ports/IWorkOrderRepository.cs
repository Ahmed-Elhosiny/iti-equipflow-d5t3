using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.WorkOrders.Ports;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid workOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetByUserIdAsync(
        Guid userId,
        WorkOrderStatus? statusFilter = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetByStatusAsync(
        WorkOrderStatus status,
        CancellationToken cancellationToken = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetByEquipmentNameAsync(string equipmentName, CancellationToken cancellationToken = default);
}
