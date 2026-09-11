namespace EquipFlow.Domain.ValueObjects;

public record DocumentMetadata(
    string Source,
    string Section,
    int? PageNumber,
    string Version,
    string Format);
