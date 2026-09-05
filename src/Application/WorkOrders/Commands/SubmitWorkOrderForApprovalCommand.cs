namespace EquipFlow.Application.WorkOrders.Commands;

public record SubmitWorkOrderForApprovalCommand(
    Guid WorkOrderId,
    string SubmittedBy);
