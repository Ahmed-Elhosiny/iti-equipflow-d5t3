namespace EquipFlow.Application.WorkOrders.Commands;

public record DispatchWorkOrderCommand(
    Guid WorkOrderId,
    string DispatcherUserId);
