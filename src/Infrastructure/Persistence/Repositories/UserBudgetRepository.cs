using EquipFlow.Application.Budget.Ports;
using EquipFlow.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence.Repositories;

public sealed class UserBudgetRepository(EquipFlowDbContext context) : IUserBudgetRepository
{
    public Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
        context.UserBudgets.FirstOrDefaultAsync(budget => budget.UserId == userId, ct);

    public async Task AddAsync(UserBudget budget, CancellationToken ct)
    {
        await context.UserBudgets.AddAsync(budget, ct);
    }

    public Task UpdateAsync(UserBudget budget, CancellationToken ct)
    {
        context.UserBudgets.Update(budget);
        return Task.CompletedTask;
    }
}