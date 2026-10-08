using EquipFlow.Domain.Enums;

namespace EquipFlow.Domain.Entities;

public class BudgetIncreaseRequest
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public BudgetRequestStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public BudgetIncreaseRequest(Guid userId, decimal requestedAmount, string reason)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        RequestedAmount = requestedAmount;
        Reason = reason;
        Status = BudgetRequestStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid reviewerId)
    {
        if (Status != BudgetRequestStatus.Pending) 
            throw new InvalidOperationException("Only pending requests can be approved.");
            
        Status = BudgetRequestStatus.Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(Guid reviewerId)
    {
        if (Status != BudgetRequestStatus.Pending) 
            throw new InvalidOperationException("Only pending requests can be rejected.");
            
        Status = BudgetRequestStatus.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}