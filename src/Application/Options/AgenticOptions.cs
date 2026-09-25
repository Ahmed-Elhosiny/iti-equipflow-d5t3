namespace EquipFlow.Application.Options;

public sealed class AgenticOptions
{
    public const string SectionName = "Agentic";

    /// <summary>
    /// Maximum number of tool-calling iterations allowed per agent before triggering the iteration breaker.
    /// ADR-001 and AG-008 mandate a default limit of 3.
    /// </summary>
    public int MaxIterations { get; set; } = 3;
}