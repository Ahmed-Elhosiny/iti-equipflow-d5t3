using EquipFlow.Application.Ports;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public class BudgetIncreaseRequestRepository(EquipFlowDbContext context) : IBudgetIncreaseRequestRepository
{
    public async Task AddAsync(BudgetIncreaseRequest request, CancellationToken ct)
    {
        context.BudgetIncreaseRequests.Add(request);
        await context.SaveChangesAsync(ct);
    }

    public async Task<BudgetIncreaseRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.BudgetIncreaseRequests.FindAsync([id], ct);

    public async Task UpdateAsync(BudgetIncreaseRequest request, CancellationToken ct)
    {
        context.BudgetIncreaseRequests.Update(request);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BudgetIncreaseRequest>> GetPendingAsync(CancellationToken ct) =>
        await context.BudgetIncreaseRequests
            .Where(r => r.Status == BudgetRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
}