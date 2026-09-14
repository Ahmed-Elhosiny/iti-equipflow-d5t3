namespace EquipFlow.Application.Agents.Contracts;

/// <summary>
/// Provides the equipment faults and available context needed to plan industrial maintenance diagnostics and safety controls.
/// </summary>
public record DiagnosticSafetyPlannerInput(
    Guid EquipmentId,
    IReadOnlyList<string> MatchedFaults,
    string? EquipmentContext);

/// <summary>
/// Contains the diagnostic actions and safety prerequisites for an industrial equipment maintenance work order.
/// </summary>
public record DiagnosticSafetyPlannerOutput(
    IReadOnlyList<DiagnosticSafetyPlannerOutput.DiagnosticStep> DiagnosticSteps,
    IReadOnlyList<DiagnosticSafetyPlannerOutput.SafetyPrerequisite> SafetyPrerequisites)
{
    /// <summary>
    /// Describes a diagnostic action to perform when investigating an industrial equipment fault.
    /// </summary>
    public record DiagnosticStep(
        string StepDescription,
        string? RequiredTool);

    /// <summary>
    /// Defines a safety condition to address before or during industrial maintenance work.
    /// </summary>
    public record SafetyPrerequisite(
        string PrerequisiteName,
        bool IsMandatory,
        string? HazardCategory);
}
