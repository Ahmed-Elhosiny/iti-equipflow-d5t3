using EquipFlow.Application.Budget.Ports;
using EquipFlow.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class UserBudgetRepository(EquipFlowDbContext context) : IUserBudgetRepository
{
    public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
        context.UserBudgets
            .Include(budget => budget.Reservations)
            .FirstOrDefaultAsync(budget => budget.UserId == userId, ct);

    public async Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken ct) =>
        await context.UserBudgets
            .Include(budget => budget.Reservations)
            .ToListAsync(ct);

    public async Task AddAsync(UserBudget budget, CancellationToken ct)
    {
        await context.UserBudgets.AddAsync(budget, ct);
    }

    public async Task UpdateAsync(UserBudget budget, CancellationToken ct)
    {
        await context.SaveChangesAsync(ct);
    }
}