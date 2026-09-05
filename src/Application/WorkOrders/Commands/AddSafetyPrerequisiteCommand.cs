namespace EquipFlow.Application.WorkOrders.Commands;

public record AddSafetyPrerequisiteCommand(
    Guid WorkOrderId,
    string Description,
    bool IsMandatory,
    int SortOrder);
