namespace EquipFlow.Application.WorkOrders.Commands;

public record ReviewWorkOrderCommand(
    Guid WorkOrderId,
    WorkOrderReviewDecision Decision,
    string ReviewerUserId,
    string? Comment = null);
