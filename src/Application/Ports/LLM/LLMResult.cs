namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Represents the final result of an LLM completion.
/// </summary>
/// <param name="Content">The generated text content, when present.</param>
/// <param name="ToolCalls">The tool calls requested by the LLM, when present.</param>
/// <param name="FinishReason">The reason generation finished.</param>
/// <param name="PromptTokens">The number of tokens in the prompt.</param>
/// <param name="CompletionTokens">The number of tokens in the completion.</param>
public sealed record LLMResult(
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls,
    string FinishReason,
    int PromptTokens,
    int CompletionTokens);
