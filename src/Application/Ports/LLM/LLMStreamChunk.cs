namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Represents a streaming delta from an LLM completion.
/// </summary>
/// <param name="DeltaContent">The incremental generated text, when present.</param>
/// <param name="DeltaToolCalls">The incremental tool calls, when present.</param>
/// <param name="IsFinished">Whether this chunk completes the stream.</param>
/// <param name="FinishReason">The reason generation finished, when present.</param>
public sealed record LLMStreamChunk(
    string? DeltaContent,
    IReadOnlyList<ToolCall>? DeltaToolCalls,
    bool IsFinished,
    string? FinishReason);
