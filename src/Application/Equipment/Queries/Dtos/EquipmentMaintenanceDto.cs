namespace EquipFlow.Application.Equipment.Queries.Dtos;

public sealed record EquipmentMaintenanceDto(
    Guid WorkOrderId,
    string Title,
    string Symptom,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string? DecisionComment);