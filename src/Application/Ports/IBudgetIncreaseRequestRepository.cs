using EquipFlow.Domain.Entities;

namespace EquipFlow.Application.Ports;

public interface IBudgetIncreaseRequestRepository
{
    Task AddAsync(BudgetIncreaseRequest request, CancellationToken ct);
    Task<BudgetIncreaseRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(BudgetIncreaseRequest request, CancellationToken ct);
    Task<IReadOnlyList<BudgetIncreaseRequest>> GetPendingAsync(CancellationToken ct);
}