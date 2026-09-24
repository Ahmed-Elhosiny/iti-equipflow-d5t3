using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Domain.Budget;

public class RunSpend
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid UserId { get; private set; }
    public Money ReservedAmount { get; private set; }
    public Money ActualAmount { get; private set; }
    public string ModelUsed { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public RunSpend(Guid runId, Guid userId, Money reservedAmount, Money actualAmount, string modelUsed)
    {
        Id = Guid.NewGuid();
        RunId = runId;
        UserId = userId;
        ReservedAmount = reservedAmount;
        ActualAmount = actualAmount;
        ModelUsed = modelUsed;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}