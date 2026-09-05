using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.WorkOrders.Ports;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid workOrderId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
