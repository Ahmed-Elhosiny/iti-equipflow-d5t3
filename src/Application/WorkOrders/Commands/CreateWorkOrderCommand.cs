namespace EquipFlow.Application.WorkOrders.Commands;

public record CreateWorkOrderCommand(
    string Title,
    string Symptom,
    string EquipmentName,
    string CreatedBy,
    string? EquipmentAssetNumber = null,
    string? ManualRevision = null,
    string? Location = null);
