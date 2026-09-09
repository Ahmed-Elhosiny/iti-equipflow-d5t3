namespace EquipFlow.Domain.Budget.ValueObjects;

public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    private Money(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount cannot be negative.");
        }

        Amount = amount;
    }

    public static Money FromDecimal(decimal amount) => new(amount);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public static Money operator +(Money left, Money right) => FromDecimal(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => FromDecimal(left.Amount - right.Amount);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;
}