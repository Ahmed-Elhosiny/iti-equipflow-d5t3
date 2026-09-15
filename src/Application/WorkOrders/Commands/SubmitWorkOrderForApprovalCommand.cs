namespace EquipFlow.Application.WorkOrders.Commands;

using MediatR;

public record SubmitWorkOrderForApprovalCommand(
    Guid WorkOrderId,
    string SubmittedBy,
    string? CorrelationId = null) : IRequest;
