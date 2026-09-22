using MediatR;

namespace EquipFlow.Application.WorkOrders.Commands;

public record ReviewWorkOrderCommand(
    Guid WorkOrderId,
    WorkOrderReviewDecision Decision,
    string ReviewerUserId,
    string? Comment = null,
    string? CorrelationId = null,
    string? Title = null,
    string? Symptom = null,
    string? EquipmentName = null,
    string? EquipmentAssetNumber = null,
    string? ManualRevision = null,
    string? Location = null) : IRequest;