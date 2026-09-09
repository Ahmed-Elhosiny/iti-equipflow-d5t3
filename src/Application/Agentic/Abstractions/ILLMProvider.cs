using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EquipFlow.Application.Agentic.Abstractions;

public interface ILLMProvider
{
    Task<CompletionResult> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChunk> StreamAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default);

    Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<ToolExecutionResult> ExecuteToolAsync(
        ToolCallRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CompletionRequest(
    string Prompt,
    string? SystemPrompt = null,
    string? Model = null,
    IReadOnlyList<ToolDefinition>? Tools = null,
    float Temperature = 0.2f,
    int? MaxTokens = null);

public sealed record CompletionResult(
    string Text,
    TokenUsage Usage,
    string? FinishReason = null);

public sealed record StreamingChunk(
    string Text,
    bool IsComplete = false);

public sealed record EmbeddingResult(
    IReadOnlyList<float> Vector,
    TokenUsage? Usage = null);

public sealed record TokenUsage(
    int InputTokens,
    int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema);

public sealed record ToolCallRequest(
    string Name,
    string ArgumentsJson);

public sealed record ToolExecutionResult(
    bool Succeeded,
    string? Result = null,
    string? Error = null);