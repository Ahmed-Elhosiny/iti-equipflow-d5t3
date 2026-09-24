using EquipFlow.Domain.Budget;

namespace EquipFlow.Application.Budget.Ports;

public interface IRunSpendRepository
{
    Task AddAsync(RunSpend spend, CancellationToken ct);
    Task<IEnumerable<RunSpend>> GetByUserIdAsync(Guid userId, CancellationToken ct);
}