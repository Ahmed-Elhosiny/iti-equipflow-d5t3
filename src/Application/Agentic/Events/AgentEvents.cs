namespace EquipFlow.Application.Agentic.Events;

/// <summary>
/// Common immutable metadata for an application-layer agent observability event.
/// Supports correlation and step-level tracing required by OBS-002.
/// </summary>
/// <param name="CorrelationId">Identifier shared by all events in an agent run.</param>
/// <param name="Timestamp">UTC timestamp at which the event occurred.</param>
/// <param name="AgentName">Name of the agent that produced the event.</param>
/// <param name="StepIndex">Zero-based step index within the agent run.</param>
public abstract record AgentEventBase(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex);

/// <summary>
/// Marks the beginning of an agent run for tracing, auditing, and timeout accounting.
/// Satisfies OBS-002 and provides the run context used by CG-006 and CG-007.
/// </summary>
/// <param name="InputSummary">Safe summary of the input supplied to the run.</param>
/// <param name="TimeoutMs">Configured maximum run duration in milliseconds.</param>
public sealed record AgentRunStarted(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex,
    string InputSummary,
    int TimeoutMs,
    string UserId = "system")
    : AgentEventBase(CorrelationId, Timestamp, AgentName, StepIndex);

/// <summary>
/// Marks the end of an agent run with its outcome, duration, and failure details.
/// Satisfies OBS-002, CG-006, CG-007, and EVAL-010.
/// </summary>
/// <param name="Status">Final status of the agent run.</param>
/// <param name="DurationMs">Elapsed run duration in milliseconds.</param>
/// <param name="ErrorMessage">Optional failure or degradation detail.</param>
/// <param name="OutputSummary">Optional safe summary of the run output.</param>
public sealed record AgentRunCompleted(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex,
    AgentRunStatus Status,
    long DurationMs,
    string? ErrorMessage,
    string? OutputSummary)
    : AgentEventBase(CorrelationId, Timestamp, AgentName, StepIndex);

/// <summary>
/// Records completed language-model usage and estimated cost for reconciliation and attribution.
/// Satisfies OBS-002, CG-006, and CG-007.
/// </summary>
/// <param name="ProviderName">Language-model provider used for the call.</param>
/// <param name="ModelIdentifier">Provider-specific model identifier.</param>
/// <param name="PromptTokens">Number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">Number of completion tokens consumed.</param>
/// <param name="TotalTokens">Total tokens consumed by the call.</param>
/// <param name="EstimatedCostUsd">Estimated cost of the call in US dollars.</param>
/// <param name="DurationMs">Elapsed call duration in milliseconds.</param>
/// <param name="WasRetried">Whether the call was retried.</param>
/// <param name="RetryReason">Optional reason for retrying the call.</param>
public sealed record LlmCallCompleted(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex,
    string ProviderName,
    string ModelIdentifier,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd,
    long DurationMs,
    bool WasRetried,
    string? RetryReason)
    : AgentEventBase(CorrelationId, Timestamp, AgentName, StepIndex);

/// <summary>
/// Records a tool invocation outcome for step-level tracing, auditing, and failure analysis.
/// Satisfies OBS-002 and EVAL-010.
/// </summary>
/// <param name="ToolName">Name of the invoked tool.</param>
/// <param name="ToolCallId">Unique identifier for the tool call.</param>
/// <param name="Status">Outcome of the tool invocation.</param>
/// <param name="DurationMs">Elapsed invocation duration in milliseconds.</param>
/// <param name="ErrorMessage">Optional failure detail.</param>
/// <param name="InputPayload">Serialized input sent to the tool.</param>
/// <param name="OutputPayload">Optional serialized output returned by the tool.</param>
public sealed record ToolInvoked(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex,
    string ToolName,
    string ToolCallId,
    ToolInvocationStatus Status,
    long DurationMs,
    string? ErrorMessage,
    string InputPayload,
    string? OutputPayload)
    : AgentEventBase(CorrelationId, Timestamp, AgentName, StepIndex);

/// <summary>
/// Records evidence attached to an agent result for inspectable, grounded responses.
/// Satisfies OBS-002 and EVAL-010.
/// </summary>
/// <param name="ChunkId">Identifier of the retrieved source chunk.</param>
/// <param name="SourceDocument">Source document containing the cited chunk.</param>
/// <param name="RelevanceScore">Relevance score assigned to the cited chunk.</param>
/// <param name="Excerpt">Quoted or summarized evidence excerpt.</param>
public sealed record CitationAttached(
    Guid CorrelationId,
    DateTimeOffset Timestamp,
    string AgentName,
    int StepIndex,
    string ChunkId,
    string SourceDocument,
    double RelevanceScore,
    string Excerpt)
    : AgentEventBase(CorrelationId, Timestamp, AgentName, StepIndex);

/// <summary>
/// Describes the outcome of an agent run for observability and evaluation.
/// </summary>
public enum AgentRunStatus
{
    Success,
    PartialSuccess,
    Failed,
    Timeout
}

/// <summary>
/// Describes the outcome of a tool invocation for observability and evaluation.
/// </summary>
public enum ToolInvocationStatus
{
    Success,
    Failed,
    Denied,
    Timeout
}
