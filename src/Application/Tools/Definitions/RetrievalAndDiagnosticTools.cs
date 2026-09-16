namespace EquipFlow.Application.Tools.Definitions;

/// <summary>
/// Request for searching equipment manuals.
/// </summary>
public sealed record SearchManualsRequest(
    Guid? EquipmentId,
    string Query,
    string? DocumentType);

/// <summary>
/// Response from a manual search.
/// </summary>
public sealed record SearchManualsResponse(
    IReadOnlyList<SearchManualsResponse.Chunk> Chunks)
{
    /// <summary>
    /// A matching manual chunk.
    /// </summary>
    public sealed record Chunk(
      Guid ChunkId,
      string Content,
      double Score,
      Citation Citation);

    /// <summary>
    /// Citation metadata for a retrieved manual chunk.
    /// </summary>
    public sealed record Citation(
      Guid DocumentId,
      string DocumentTitle,
      int? Page,
      string? Section);
}

/// <summary>
/// Request for querying equipment fault history.
/// </summary>
public sealed record QueryFaultHistoryRequest(
    Guid EquipmentId,
    string? Symptom);

/// <summary>
/// Response from a fault history query.
/// </summary>
public sealed record QueryFaultHistoryResponse(
    IReadOnlyList<QueryFaultHistoryResponse.HistoryRecord> Records)
{
    /// <summary>
    /// A recorded equipment fault event.
    /// </summary>
    public sealed record HistoryRecord(
        Guid EventId,
        DateTime OccurredAt,
        string FaultDescription,
        string? Resolution);
}

/// <summary>
/// Request for retrieving equipment specifications.
/// </summary>
public sealed record GetEquipmentSpecsRequest(Guid EquipmentId);

/// <summary>
/// Response containing equipment specifications.
/// </summary>
public sealed record GetEquipmentSpecsResponse(
    string EquipmentName,
    string Model,
    string Manufacturer,
    string? OperatingLimits);

/// <summary>
/// Request for generating an equipment safety checklist.
/// </summary>
public sealed record GenerateSafetyChecklistRequest(
    Guid EquipmentId,
  string TaskDescription);

/// <summary>
/// Response containing a generated safety checklist.
/// </summary>
public sealed record GenerateSafetyChecklistResponse(
    IReadOnlyList<GenerateSafetyChecklistResponse.ChecklistItem> Items)
{
    /// <summary>
    /// An item in the generated safety checklist.
    /// </summary>
    public sealed record ChecklistItem(
      string Description,
      bool IsMandatory,
      ChecklistCitation Citation);

    /// <summary>
    /// Citation metadata for a safety checklist item.
    /// </summary>
    public sealed record ChecklistCitation(
      Guid DocumentId,
      string DocumentTitle,
      int? Page,
      string? Section);
}

/// <summary>
/// JSON schemas for retrieval and diagnostic tool requests.
/// </summary>
public static class ToolSchemas
{
    /// <summary>
    /// JSON schema for <see cref="SearchManualsRequest"/>.
    /// </summary>
    public const string SearchManualsSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "SearchManualsRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": ["string", "null"],
              "format": "uuid"
            },
            "query": {
              "type": "string"
            },
            "documentType": {
              "type": ["string", "null"]
            }
          },
          "required": ["query"]
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="QueryFaultHistoryRequest"/>.
    /// </summary>
    public const string QueryFaultHistorySchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "QueryFaultHistoryRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string",
              "format": "uuid"
            },
            "symptom": {
              "type": ["string", "null"]
            }
          },
          "required": ["equipmentId"]
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="GetEquipmentSpecsRequest"/>.
    /// </summary>
    public const string GetEquipmentSpecsSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "GetEquipmentSpecsRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string",
              "format": "uuid"
            }
          },
          "required": ["equipmentId"]
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="GenerateSafetyChecklistRequest"/>.
    /// </summary>
    public const string GenerateSafetyChecklistSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "GenerateSafetyChecklistRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string",
              "format": "uuid"
            },
            "taskDescription": {
              "type": "string"
            }
          },
          "required": ["equipmentId", "taskDescription"]
        }
        """;
}
