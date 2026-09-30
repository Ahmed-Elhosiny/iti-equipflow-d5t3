using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using System.Reflection;

namespace EquipFlow.Application.Tests.Fakes;

public class FakeWorkOrderRepository : IWorkOrderRepository
{
    private readonly Dictionary<Guid, WorkOrder> _workOrders = new();
    private readonly List<SafetyPrerequisite> _safetyPrerequisites = new();
    public bool SaveChangesCalled { get; private set; }

    public Task<WorkOrder?> GetByIdAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetReconstructedWorkOrder(workOrderId));
    }

    public Task<WorkOrder?> GetByIdNoTrackingAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetReconstructedWorkOrder(workOrderId));
    }

    public Task<IReadOnlyList<WorkOrder>> GetByEquipmentNameAsync(string equipmentName, CancellationToken cancellationToken = default)
    {
        var results = _workOrders.Values
            .Where(wo => wo.EquipmentName == equipmentName)
            .Select(wo => GetReconstructedWorkOrder(wo.Id))
            .Where(wo => wo != null)
            .Cast<WorkOrder>()
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkOrder>>(results);
    }

    public Task<IReadOnlyList<WorkOrder>> GetByUserIdAsync(Guid userId, WorkOrderStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<WorkOrder> workOrders = _workOrders.Values
            .Where(workOrder => workOrder.CreatedBy == userId.ToString());
            
        if (statusFilter.HasValue)
            workOrders = workOrders.Where(workOrder => workOrder.Status == statusFilter.Value);
            
        var results = workOrders.Select(wo => GetReconstructedWorkOrder(wo.Id)).Where(wo => wo != null).Cast<WorkOrder>().ToList();
        return Task.FromResult<IReadOnlyList<WorkOrder>>(results);
    }

    public Task<IReadOnlyList<WorkOrder>> GetByStatusAsync(WorkOrderStatus status, CancellationToken cancellationToken = default)
    {
        var results = _workOrders.Values
            .Where(workOrder => workOrder.Status == status)
            .Select(wo => GetReconstructedWorkOrder(wo.Id))
            .Where(wo => wo != null)
            .Cast<WorkOrder>()
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkOrder>>(results);
    }

    public Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        _workOrders[workOrder.Id] = workOrder;
        return Task.CompletedTask;
    }

    public Task AddSafetyPrerequisiteAsync(SafetyPrerequisite prerequisite, CancellationToken cancellationToken = default)
    {
        _safetyPrerequisites.Add(prerequisite);
        return Task.CompletedTask;
    }

    public Task SaveStateTransitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        _workOrders[workOrder.Id] = workOrder;
        SaveChangesCalled = true;
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
        _safetyPrerequisites.Clear();
        SaveChangesCalled = false;
    }

    public WorkOrder? GetSavedWorkOrder(Guid id) => GetReconstructedWorkOrder(id);

    private WorkOrder? GetReconstructedWorkOrder(Guid id)
    {
        _workOrders.TryGetValue(id, out var workOrder);
        if (workOrder != null)
        {
            var safetyField = typeof(WorkOrder).GetField("_safetyPrerequisites", BindingFlags.NonPublic | BindingFlags.Instance);
            if (safetyField != null)
            {
                var collection = (ICollection<SafetyPrerequisite>)safetyField.GetValue(workOrder)!;
                collection.Clear();
                foreach (var p in _safetyPrerequisites.Where(p => p.WorkOrderId == id))
                {
                    collection.Add(p);
                }
            }
        }
        return workOrder;
    }

    public IReadOnlyCollection<SafetyPrerequisite> GetSavedSafetyPrerequisites() => _safetyPrerequisites.AsReadOnly();
    public IReadOnlyCollection<WorkOrder> GetAll() => _workOrders.Values.ToList().AsReadOnly();
}