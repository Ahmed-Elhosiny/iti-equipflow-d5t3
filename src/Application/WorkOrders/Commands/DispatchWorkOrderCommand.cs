namespace EquipFlow.Application.WorkOrders.Commands;

using MediatR;

public record DispatchWorkOrderCommand(
    Guid WorkOrderId,
    string DispatcherUserId,
    string? CorrelationId = null) : IRequest;
