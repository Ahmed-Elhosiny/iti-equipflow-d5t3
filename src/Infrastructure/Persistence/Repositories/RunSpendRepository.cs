using EquipFlow.Application.Budget.Ports;
using EquipFlow.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class RunSpendRepository(EquipFlowDbContext context) : IRunSpendRepository
{
    public async Task AddAsync(RunSpend spend, CancellationToken ct)
    {
        await context.RunSpends.AddAsync(spend, ct);
    }

    public async Task<IEnumerable<RunSpend>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
        await context.RunSpends
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
}