using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Domain.Budget.Exceptions;

public sealed class InsufficientBudgetException : Exception
{
    public Guid UserId { get; }

    public Money RequestedAmount { get; }

    public Money AvailableAmount { get; }

    public InsufficientBudgetException(
        Guid userId,
        Money requestedAmount,
        Money availableAmount)
        : base(
            $"Budget exhausted for user '{userId}'. " +
            $"Requested {requestedAmount.Amount:F2}, but only {availableAmount.Amount:F2} is available.")
    {
        UserId = userId;
        RequestedAmount = requestedAmount;
        AvailableAmount = availableAmount;
    }
}
