namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Describes a tool that an LLM may call.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">The tool description presented to the LLM.</param>
/// <param name="ParametersJsonSchema">The JSON schema describing the tool parameters.</param>
public sealed record ToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema);
