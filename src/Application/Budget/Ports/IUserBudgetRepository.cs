using EquipFlow.Domain.Budget;

namespace EquipFlow.Application.Budget.Ports;

public interface IUserBudgetRepository
{
    Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(UserBudget budget, CancellationToken ct);
    Task UpdateAsync(UserBudget budget, CancellationToken ct);
}
