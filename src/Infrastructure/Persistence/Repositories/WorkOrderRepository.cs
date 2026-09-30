using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class WorkOrderRepository(EquipFlowDbContext context) : IWorkOrderRepository
{
    public async Task<IReadOnlyList<WorkOrder>> GetByEquipmentNameAsync(
        string equipmentName, 
        CancellationToken cancellationToken = default)
    {
        return await context.WorkOrders
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .Include(workOrder => workOrder.ApprovalActions)
            .Where(wo => wo.EquipmentName == equipmentName)
            .OrderByDescending(wo => wo.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<WorkOrder?> GetByIdAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        context.WorkOrders
            .AsTracking()
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .Include(workOrder => workOrder.ApprovalActions)
            .FirstOrDefaultAsync(workOrder => workOrder.Id == workOrderId, cancellationToken);

    public Task<WorkOrder?> GetByIdNoTrackingAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        context.WorkOrders
            .AsNoTracking()
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .FirstOrDefaultAsync(workOrder => workOrder.Id == workOrderId, cancellationToken);

    public async Task<IReadOnlyList<WorkOrder>> GetByUserIdAsync(
        Guid userId,
        WorkOrderStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.WorkOrders
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .Include(workOrder => workOrder.ApprovalActions)
            .Where(workOrder => workOrder.CreatedBy == userId.ToString());

        if (statusFilter.HasValue)
        {
            query = query.Where(workOrder => workOrder.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(workOrder => workOrder.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkOrder>> GetByStatusAsync(
        WorkOrderStatus status,
        CancellationToken cancellationToken = default) =>
        await context.WorkOrders
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .Include(workOrder => workOrder.ApprovalActions)
            .Where(workOrder => workOrder.Status == status)
            .OrderByDescending(workOrder => workOrder.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default) =>
        await context.WorkOrders.AddAsync(workOrder, cancellationToken);

    public async Task AddSafetyPrerequisiteAsync(
        SafetyPrerequisite prerequisite,
        CancellationToken cancellationToken = default) =>
        await context.Set<SafetyPrerequisite>().AddAsync(prerequisite, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}