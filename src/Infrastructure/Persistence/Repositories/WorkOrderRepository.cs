using EquipFlow.Application.WorkOrders.Ports;
using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class WorkOrderRepository(EquipFlowDbContext context) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        context.WorkOrders
            .Include(workOrder => workOrder.SafetyPrerequisites)
            .Include(workOrder => workOrder.ApprovalActions)
            .FirstOrDefaultAsync(workOrder => workOrder.Id == workOrderId, cancellationToken);

    public async Task AddAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default) =>
        await context.WorkOrders.AddAsync(workOrder, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}