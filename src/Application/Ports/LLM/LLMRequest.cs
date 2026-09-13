namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Represents a request to generate an LLM completion.
/// </summary>
/// <param name="Messages">The conversation messages supplied to the LLM.</param>
/// <param name="Tools">The tools the LLM may call, when any are available.</param>
/// <param name="Temperature">The sampling temperature for generation.</param>
/// <param name="MaxTokens">The maximum number of completion tokens, when specified.</param>
public sealed record LLMRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools,
    double Temperature,
    int? MaxTokens);
