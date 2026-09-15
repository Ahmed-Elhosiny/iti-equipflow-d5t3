namespace EquipFlow.Application.WorkOrders.Commands;

using MediatR;

public record ReviewWorkOrderCommand(
    Guid WorkOrderId,
    WorkOrderReviewDecision Decision,
    string ReviewerUserId,
    string? Comment = null,
    string? CorrelationId = null) : IRequest;
