using EquipFlow.Domain.Budget;

namespace EquipFlow.Application.Budget.Ports;

public interface IUserBudgetRepository
{
    Task<UserBudget?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<UserBudget?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct);
    Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken ct);
    Task<IEnumerable<UserBudget>> GetBudgetsNeedingResetAsync(DateTimeOffset currentDate, CancellationToken ct);
    Task AddAsync(UserBudget budget, CancellationToken ct);
    Task UpdateAsync(UserBudget budget, CancellationToken ct);
}