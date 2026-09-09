using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Domain.Budget;

public class UserBudget
{
    private readonly List<BudgetReservation> _reservations = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Money TotalLimit { get; private set; }
    public Money ConsumedAmount { get; private set; }
    public Money ReservedAmount => _reservations.Aggregate(
        Money.FromDecimal(0),
        (total, reservation) => total + reservation.EstimatedCost);
    public Money AvailableAmount => TotalLimit - ConsumedAmount - ReservedAmount;

    public UserBudget(Guid userId, Money totalLimit)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TotalLimit = totalLimit;
        ConsumedAmount = Money.FromDecimal(0);
    }

    public bool TryReserve(Guid reservationId, Money estimatedCost)
    {
        if (_reservations.Any(reservation => reservation.Id == reservationId))
            return false;

        if (AvailableAmount < estimatedCost)
            return false;

        _reservations.Add(new BudgetReservation(reservationId, estimatedCost));
        return true;
    }

    public void Commit(Guid reservationId, Money actualCost)
    {
        var reservation = _reservations.FirstOrDefault(item => item.Id == reservationId)
            ?? throw new InvalidOperationException($"Budget reservation with id {reservationId} was not found.");

        var consumedAmount = ConsumedAmount + actualCost;
        if (consumedAmount + ReservedAmount - reservation.EstimatedCost > TotalLimit)
            throw new InvalidOperationException("Committing the reservation would exceed the total budget limit.");

        _reservations.Remove(reservation);
        ConsumedAmount = consumedAmount;
    }

    public void Release(Guid reservationId)
    {
        var reservation = _reservations.FirstOrDefault(item => item.Id == reservationId);
        if (reservation is not null)
            _reservations.Remove(reservation);
    }
}