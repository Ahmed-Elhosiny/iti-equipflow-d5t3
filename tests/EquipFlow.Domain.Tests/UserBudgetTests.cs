using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class UserBudgetTests
{
    [Fact]
    public void TryReserve_TracksReservedAndAvailableAmounts()
    {
        var budget = CreateBudget(100m);

        var reserved = budget.TryReserve(Guid.NewGuid(), Money.FromDecimal(40m));

        Assert.True(reserved);
        Assert.Equal(Money.FromDecimal(40m), budget.ReservedAmount);
        Assert.Equal(Money.FromDecimal(60m), budget.AvailableAmount);
    }

    [Fact]
    public void TryReserve_ReturnsFalseWhenReservationWouldExceedLimit()
    {
        var budget = CreateBudget(100m);

        var reserved = budget.TryReserve(Guid.NewGuid(), Money.FromDecimal(101m));

        Assert.False(reserved);
        Assert.Equal(Money.FromDecimal(0m), budget.ReservedAmount);
        Assert.Equal(Money.FromDecimal(100m), budget.AvailableAmount);
    }

    [Fact]
    public void Commit_RemovesReservationAndAddsActualCostToConsumedAmount()
    {
        var budget = CreateBudget(100m);
        var reservationId = Guid.NewGuid();
        budget.TryReserve(reservationId, Money.FromDecimal(40m));

        budget.Commit(reservationId, Money.FromDecimal(25m));

        Assert.Equal(Money.FromDecimal(25m), budget.ConsumedAmount);
        Assert.Equal(Money.FromDecimal(0m), budget.ReservedAmount);
        Assert.Equal(Money.FromDecimal(75m), budget.AvailableAmount);
    }

    [Fact]
    public void Commit_ThrowsWhenActualCostWouldExceedLimitWithoutChangingState()
    {
        var budget = CreateBudget(100m);
        var reservationId = Guid.NewGuid();
        budget.TryReserve(reservationId, Money.FromDecimal(40m));

        Assert.Throws<InvalidOperationException>(() => budget.Commit(reservationId, Money.FromDecimal(101m)));
        Assert.Equal(Money.FromDecimal(0m), budget.ConsumedAmount);
        Assert.Equal(Money.FromDecimal(40m), budget.ReservedAmount);
    }

    [Fact]
    public void Commit_ThrowsWhenReservationDoesNotExist()
    {
        var budget = CreateBudget(100m);

        Assert.Throws<InvalidOperationException>(() => budget.Commit(Guid.NewGuid(), Money.FromDecimal(10m)));
    }

    [Fact]
    public void Release_RemovesReservationWithoutChangingConsumedAmount()
    {
        var budget = CreateBudget(100m);
        var reservationId = Guid.NewGuid();
        budget.TryReserve(reservationId, Money.FromDecimal(40m));

        budget.Release(reservationId);

        Assert.Equal(Money.FromDecimal(0m), budget.ConsumedAmount);
        Assert.Equal(Money.FromDecimal(0m), budget.ReservedAmount);
        Assert.Equal(Money.FromDecimal(100m), budget.AvailableAmount);
    }

    [Fact]
    public void TryReset_ResetsConsumedAmountAndClearsReservations()
    {
        var budget = CreateBudget(100m, 15);
        var reservationId = Guid.NewGuid();
        budget.TryReserve(reservationId, Money.FromDecimal(40m));
        budget.Commit(reservationId, Money.FromDecimal(20m));

        // Force reset by passing a date far in the future
        var result = budget.TryReset(DateTimeOffset.UtcNow.AddMonths(2), 15);

        Assert.True(result);
        Assert.Equal(Money.FromDecimal(0m), budget.ConsumedAmount);
        Assert.Empty(budget.Reservations);
    }

    [Fact]
    public void TryReset_ReturnsFalseIfCurrentDateIsBeforeNextResetDate()
    {
        var budget = CreateBudget(100m, 15);
        
        // Assuming NextResetDate is in the future, passing today should fail
        var result = budget.TryReset(DateTimeOffset.UtcNow.AddDays(-1), 15);

        Assert.False(result);
    }

    [Fact]
    public void CalculateNextResetDate_ClampsToLastDayOfMonth_When31stInShortMonth()
    {
        var budget = CreateBudget(100m, 31);
        
        // Move to next month to trigger calculation for a potentially shorter month
        var futureDate = budget.NextResetDate.AddMonths(1); 
        budget.TryReset(futureDate, 31);
        
        var maxDay = DateTime.DaysInMonth(budget.NextResetDate.Year, budget.NextResetDate.Month);
        Assert.Equal(Math.Min(31, maxDay), budget.NextResetDate.Day);
    }

    private static UserBudget CreateBudget(decimal totalLimit, int resetDayOfMonth = 1) =>
        new(Guid.NewGuid(), Money.FromDecimal(totalLimit), resetDayOfMonth);
}