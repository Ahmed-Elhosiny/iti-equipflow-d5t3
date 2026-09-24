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

    public async Task<UserBudget?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct)
    {
        var tableName = context.Model.FindEntityType(typeof(UserBudget))?.GetTableName() ?? "UserBudgets";
        var sql = $"SELECT * FROM \"{tableName}\" WHERE \"UserId\" = {{0}} FOR UPDATE";
        
        return await context.UserBudgets
            .FromSqlRaw(sql, userId)
            .Include(b => b.Reservations)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<UserBudget>> GetAllAsync(CancellationToken ct) =>
        await context.UserBudgets
            .Include(budget => budget.Reservations)
            .ToListAsync(ct);

    public async Task<IEnumerable<UserBudget>> GetBudgetsNeedingResetAsync(DateTimeOffset currentDate, CancellationToken ct) =>
        await context.UserBudgets
            .Where(budget => budget.NextResetDate <= currentDate)
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