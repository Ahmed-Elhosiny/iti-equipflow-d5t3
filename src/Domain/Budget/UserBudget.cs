using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Domain.Budget;

public class UserBudget
{
    private readonly List<BudgetReservation> _reservations = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Money TotalLimit { get; private set; }
    public Money ConsumedAmount { get; private set; }
    public DateTimeOffset NextResetDate { get; private set; }
    public IReadOnlyCollection<BudgetReservation> Reservations => _reservations.AsReadOnly();
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
        NextResetDate = CalculateNextResetDate(DateTimeOffset.UtcNow);
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

    public bool TryReset(DateTimeOffset currentDate)
    {
        if (currentDate < NextResetDate)
            return false;

        ConsumedAmount = Money.FromDecimal(0);
        NextResetDate = CalculateNextResetDate(currentDate);
        
        // Clear any stale reservations that might have been left behind
        _reservations.Clear();
        
        return true;
    }

    private static DateTimeOffset CalculateNextResetDate(DateTimeOffset currentDate) =>
        new DateTimeOffset(currentDate.Year, currentDate.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
}