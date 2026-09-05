namespace EquipFlow.Application.WorkOrders.Commands;

public record CompleteSafetyPrerequisiteCommand(
    Guid WorkOrderId,
    Guid PrerequisiteId,
    string CompletedBy,
    string? CompletionNote = null);
