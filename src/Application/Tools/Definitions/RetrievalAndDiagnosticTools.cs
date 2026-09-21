namespace EquipFlow.Application.Tools.Definitions;

/// <summary>
/// Request for searching equipment manuals.
/// </summary>
public sealed record SearchManualsRequest(
    string? EquipmentId, // Changed from Guid?
    string Query,
    string? DocumentType);

/// <summary>
/// Response from a manual search.
/// </summary>
public sealed record SearchManualsResponse(
    IReadOnlyList<SearchManualsResponse.Chunk> Chunks)
{
    public sealed record Chunk(
      Guid ChunkId,
      string Content,
      double Score,
      Citation Citation);

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
    string EquipmentId,
    string? Symptom);

/// <summary>
/// Response from a fault history query.
/// </summary>
public sealed record QueryFaultHistoryResponse(
    IReadOnlyList<QueryFaultHistoryResponse.HistoryRecord> Records)
{
    public sealed record HistoryRecord(
        Guid EventId,
        DateTime OccurredAt,
        string FaultDescription,
        string? Resolution);
}

/// <summary>
/// Request for retrieving equipment specifications.
/// </summary>
public sealed record GetEquipmentSpecsRequest(string EquipmentId); // Changed from Guid

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
    string EquipmentId, // Changed from Guid
    string TaskDescription);

/// <summary>
/// Response containing a generated safety checklist.
/// </summary>
public sealed record GenerateSafetyChecklistResponse(
    IReadOnlyList<GenerateSafetyChecklistResponse.ChecklistItem> Items)
{
    public sealed record ChecklistItem(
      string Description,
      bool IsMandatory,
      ChecklistCitation Citation);

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
    public const string SearchManualsSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "SearchManualsRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": ["string", "null"]
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

    public const string QueryFaultHistorySchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "QueryFaultHistoryRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string"
            },
            "symptom": {
              "type": ["string", "null"]
            }
          },
          "required": ["equipmentId"]
        }
        """;

    public const string GetEquipmentSpecsSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "GetEquipmentSpecsRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string"
            }
          },
          "required": ["equipmentId"]
        }
        """;

    public const string GenerateSafetyChecklistSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "GenerateSafetyChecklistRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string"
            },
            "taskDescription": {
              "type": "string"
            }
          },
          "required": ["equipmentId", "taskDescription"]
        }
        """;
}