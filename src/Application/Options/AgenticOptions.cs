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
}