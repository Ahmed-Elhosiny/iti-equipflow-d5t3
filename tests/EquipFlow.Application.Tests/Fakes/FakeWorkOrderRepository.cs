using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;

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
