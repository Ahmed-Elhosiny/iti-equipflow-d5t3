namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Represents an LLM request to call a tool.
/// </summary>
/// <param name="Id">The tool call identifier.</param>
/// <param name="Name">The name of the tool to call.</param>
/// <param name="ArgumentsJson">The tool arguments serialized as JSON.</param>
public sealed record ToolCall(
    string Id,
    string Name,
    string ArgumentsJson);
