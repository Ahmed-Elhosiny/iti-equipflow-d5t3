namespace EquipFlow.Application.Documents.Queries.Dtos;

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string Type,
    string Status,
    string Source,
    string Section,
    string Version,
    string Format,
    DateTime CreatedAt);