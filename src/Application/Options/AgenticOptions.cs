namespace EquipFlow.Application.Options;

public sealed class AgenticOptions
{
    public const string SectionName = "Agentic";

    /// <summary>
    /// Maximum number of tool-calling iterations allowed per agent before triggering the iteration breaker.
    /// ADR-001 and AG-008 mandate a default limit of 3.
    /// </summary>
    public int MaxIterations { get; set; } = 3;

    /// <summary>
    /// Maximum number of JSON schema parsing retries allowed per agent after a malformed LLM output.
    /// AG-010 / FR-030 mandate a strict bounded retry limit of 1 (allowing 2 total attempts).
    /// </summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>
    /// Minimum relevance score (0.0 to 1.0) required for a retrieved chunk to be considered grounded evidence.
    /// Chunks below this threshold are discarded, triggering the refusal path (FR-2).
    /// Tuned to 0.60 to accommodate local embedding models (mxbai-embed-large) and synthetic seeder text.
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.60;

    /// <summary>
    /// Maximum number of retries for transient LLM network/rate-limit errors before cascading to the next provider (FR-5 / ADR-001).
    /// </summary>
    public int MaxLlmRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff on transient LLM errors.
    /// Actual delay = BaseDelay * 2^attempt.
    /// </summary>
    public int LlmRetryBaseDelayMs { get; set; } = 500;

    /// <summary>
    /// Timeout in seconds for a single agent step execution (AG-009).
    /// Increased to 120s to accommodate local LLM inference latency (Ollama on CPU).
    /// </summary>
    public int StepTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Global timeout in seconds for the entire agentic workflow (AG-009).
    /// </summary>
    public int GlobalTimeoutSeconds { get; set; } = 300;
}