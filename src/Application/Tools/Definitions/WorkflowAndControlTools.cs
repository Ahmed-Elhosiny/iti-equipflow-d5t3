namespace EquipFlow.Application.Tools.Definitions;

/// <summary>
/// Request for drafting a work order.
/// </summary>
public sealed record DraftWorkOrderRequest(
    Guid EquipmentId,
    string Title,
    string Description,
    decimal EstimatedCost,
    IReadOnlyList<string> RequiredParts,
    IReadOnlyList<string> SafetyPrerequisites);

/// <summary>
/// Response from drafting a work order.
/// </summary>
public sealed record DraftWorkOrderResponse(bool Success, string Status);

/// <summary>
/// Request for validating an estimated work order cost with the Cost Governor.
/// </summary>
public sealed record ValidateBudgetRequest(
  Guid EquipmentId,
  int? EstimatedTokens = null,
  decimal? EstimatedCost = null);

/// <summary>
/// Response from the Cost Governor budget validation.
/// </summary>
public sealed record ValidateBudgetResponse(
  bool IsApproved,
  decimal ReservedAmount,
  string? Reason);

/// <summary>
/// Request for checking a work order's Approval Gate status.
/// </summary>
public sealed record CheckApprovalStatusRequest(Guid WorkOrderId);

/// <summary>
/// Response from the Approval Gate status check.
/// </summary>
public sealed record CheckApprovalStatusResponse(
    string Status,
    string? ApprovedBy,
    DateTime? ApprovedAt);

/// <summary>
/// Request for emitting an observability event from an agent.
/// </summary>
public sealed record EmitAgentEventRequest(
    string AgentName,
    string EventName,
    string Details);

/// <summary>
/// Response from emitting an agent observability event.
/// </summary>
public sealed record EmitAgentEventResponse(bool Success);

/// <summary>
/// JSON schemas for workflow and control tool requests.
/// </summary>
public static class WorkflowToolSchemas
{
    /// <summary>
    /// JSON schema for <see cref="DraftWorkOrderRequest"/>.
    /// </summary>
    public const string DraftWorkOrderSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "DraftWorkOrderRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string",
              "format": "uuid"
            },
            "title": {
              "type": "string"
            },
            "description": {
              "type": "string"
            },
            "estimatedCost": {
              "type": "number"
            },
            "requiredParts": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "safetyPrerequisites": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          "required": [
            "equipmentId",
            "title",
            "description",
            "estimatedCost",
            "requiredParts",
            "safetyPrerequisites"
          ]
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="ValidateBudgetRequest"/>.
    /// </summary>
    public const string ValidateBudgetSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "ValidateBudgetRequest",
          "type": "object",
          "properties": {
            "equipmentId": {
              "type": "string",
              "format": "uuid"
            },
            "estimatedTokens": {
              "type": "integer",
              "minimum": 1
            },
            "estimatedCost": {
              "type": "number",
              "minimum": 0
            }
          },
          "required": ["equipmentId"],
          "oneOf": [
            { "required": ["estimatedTokens"] },
            { "required": ["estimatedCost"] }
          ],
          "not": {
            "required": ["estimatedTokens", "estimatedCost"]
          }
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="CheckApprovalStatusRequest"/>.
    /// </summary>
    public const string CheckApprovalStatusSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "CheckApprovalStatusRequest",
          "type": "object",
          "properties": {
            "workOrderId": {
              "type": "string",
              "format": "uuid"
            }
          },
          "required": ["workOrderId"]
        }
        """;

    /// <summary>
    /// JSON schema for <see cref="EmitAgentEventRequest"/>.
    /// </summary>
    public const string EmitAgentEventSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "EmitAgentEventRequest",
          "type": "object",
          "properties": {
            "agentName": {
              "type": "string"
            },
            "eventName": {
              "type": "string"
            },
            "details": {
              "type": "string"
            }
          },
          "required": ["agentName", "eventName", "details"]
        }
        """;
}