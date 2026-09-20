namespace EquipFlow.Application.Equipment.Queries.Dtos;

public sealed record EquipmentDto(
    Guid Id,
    string Name,
    string? SerialNumber,
    string? Line);