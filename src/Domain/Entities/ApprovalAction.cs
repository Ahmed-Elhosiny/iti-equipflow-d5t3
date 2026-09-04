namespace EquipFlow.Domain.Entities;

public class ApprovalAction
{
    public Guid Id { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Enums.ApprovalActionType ActionType { get; private set; }
    public string ActorUserId { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    // EF Core parameterless constructor
    private ApprovalAction()
    {
        Id = Guid.Empty;
        WorkOrderId = Guid.Empty;
        ActionType = 0;
        ActorUserId = string.Empty;
        OccurredAtUtc = DateTimeOffset.MinValue;
    }

    public ApprovalAction(Guid workOrderId, Enums.ApprovalActionType actionType, string actorUserId, string? comment = null)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("WorkOrderId cannot be empty.", nameof(workOrderId));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId cannot be empty.", nameof(actorUserId));

        Id = Guid.NewGuid();
        WorkOrderId = workOrderId;
        ActionType = actionType;
        ActorUserId = actorUserId;
        Comment = comment;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }
}
