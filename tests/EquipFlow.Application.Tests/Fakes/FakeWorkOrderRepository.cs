using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Application.Tests.Fakes;

public class FakeWorkOrderRepository : IWorkOrderRepository
{
    private readonly Dictionary<Guid, WorkOrder> _workOrders = new();
    public bool SaveChangesCalled { get; private set; }

    public Task<WorkOrder?> GetByIdAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        _workOrders.TryGetValue(workOrderId, out var workOrder);
        return Task.FromResult(workOrder);
    }
        public Task<IReadOnlyList<WorkOrder>> GetByEquipmentNameAsync(
        string equipmentName, 
        CancellationToken cancellationToken = default)
    {
        // Assuming your fake uses a List<WorkOrder> or similar collection named _workOrders
        var results = _workOrders.Values
            .Where(wo => wo.EquipmentName == equipmentName)
            .ToList();
            
        return Task.FromResult<IReadOnlyList<WorkOrder>>(results);
    }
    public Task<IReadOnlyList<WorkOrder>> GetByUserIdAsync(
        Guid userId,
        WorkOrderStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<WorkOrder> workOrders = _workOrders.Values
            .Where(workOrder => workOrder.CreatedBy == userId.ToString());
        if (statusFilter.HasValue)
            workOrders = workOrders.Where(workOrder => workOrder.Status == statusFilter.Value);
        return Task.FromResult<IReadOnlyList<WorkOrder>>(workOrders.ToList());
    }

    public Task<IReadOnlyList<WorkOrder>> GetByStatusAsync(
        WorkOrderStatus status,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkOrder>>(
            _workOrders.Values.Where(workOrder => workOrder.Status == status).ToList());

    public Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        _workOrders[workOrder.Id] = workOrder;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalled = true;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _workOrders.Clear();
        SaveChangesCalled = false;
    }

    public WorkOrder? GetSavedWorkOrder(Guid id)
    {
        _workOrders.TryGetValue(id, out var workOrder);
        return workOrder;
    }

    public IReadOnlyCollection<WorkOrder> GetAll() => _workOrders.Values.ToList().AsReadOnly();
}
