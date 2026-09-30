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
            .Include(workOrder => workOrder.ApprovalActions)
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

    public async Task SaveStateTransitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        // Use ExecuteUpdateAsync to bypass change tracker concurrency issues with PostgreSQL timestamp precision
        await context.WorkOrders
            .Where(wo => wo.Id == workOrder.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(wo => wo.Status, workOrder.Status)
                .SetProperty(wo => wo.UpdatedAtUtc, workOrder.UpdatedAtUtc)
                .SetProperty(wo => wo.DecisionBy, workOrder.DecisionBy)
                .SetProperty(wo => wo.DecisionAtUtc, workOrder.DecisionAtUtc)
                .SetProperty(wo => wo.DecisionComment, workOrder.DecisionComment)
                .SetProperty(wo => wo.Title, workOrder.Title)
                .SetProperty(wo => wo.Symptom, workOrder.Symptom)
                .SetProperty(wo => wo.EquipmentName, workOrder.EquipmentName)
                .SetProperty(wo => wo.EquipmentAssetNumber, workOrder.EquipmentAssetNumber)
                .SetProperty(wo => wo.ManualRevision, workOrder.ManualRevision)
                .SetProperty(wo => wo.Location, workOrder.Location),
                cancellationToken);

        // Update any modified child entities (e.g., SafetyPrerequisites)
        foreach (var sp in workOrder.SafetyPrerequisites)
        {
            await context.Set<SafetyPrerequisite>()
                .Where(s => s.Id == sp.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.CompletedAtUtc, sp.CompletedAtUtc)
                    .SetProperty(s => s.CompletedBy, sp.CompletedBy)
                    .SetProperty(s => s.CompletionNote, sp.CompletionNote),
                    cancellationToken);
        }

        // Add any new ApprovalActions generated by the domain methods
        var existingActionIds = await context.Set<ApprovalAction>()
            .Where(a => a.WorkOrderId == workOrder.Id)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var newActions = workOrder.ApprovalActions
            .Where(a => !existingActionIds.Contains(a.Id))
            .ToList();

        if (newActions.Any())
        {
            await context.Set<ApprovalAction>().AddRangeAsync(newActions, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}