namespace EquipFlow.Domain.Search;

public sealed record SearchFilters
{
    public string? EquipmentId { get; }

    public string? DocumentType { get; }

    public string? ProductionLine { get; }

    public string? DocumentVersion { get; }

    public bool HasAny => EquipmentId is not null
        || DocumentType is not null
        || ProductionLine is not null
        || DocumentVersion is not null;

    private SearchFilters(
        string? equipmentId,
        string? documentType,
        string? productionLine,
        string? documentVersion)
    {
        EquipmentId = ValidateOptional(equipmentId, nameof(equipmentId));
        DocumentType = ValidateOptional(documentType, nameof(documentType));
        ProductionLine = ValidateOptional(productionLine, nameof(productionLine));
        DocumentVersion = ValidateOptional(documentVersion, nameof(documentVersion));
    }

    public static SearchFilters Create(
        string? equipmentId = null,
        string? documentType = null,
        string? productionLine = null,
        string? documentVersion = null) =>
        new(equipmentId, documentType, productionLine, documentVersion);

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return value;
    }
}