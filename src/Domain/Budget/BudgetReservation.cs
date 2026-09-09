using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Domain.Budget;

public class BudgetReservation
{
    public Guid Id { get; private set; }
    public Money EstimatedCost { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public BudgetReservation(Guid id, Money estimatedCost)
    {
        Id = id;
        EstimatedCost = estimatedCost;
        CreatedAt = DateTime.UtcNow;
    }
}