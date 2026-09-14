namespace EquipFlow.Application.Agents.Contracts;

/// <summary>
/// Provides the diagnostic findings, safety controls, and maintenance context needed to generate an industrial equipment work order.
/// </summary>
public record WorkOrderGeneratorInput(
    Guid EquipmentId,
    IReadOnlyList<DiagnosticSafetyPlannerOutput.DiagnosticStep> DiagnosticSteps,
    IReadOnlyList<DiagnosticSafetyPlannerOutput.SafetyPrerequisite> SafetyPrerequisites,
    string? AdditionalNotes);

/// <summary>
/// Represents the proposed industrial maintenance work order, including its scope, estimated cost, and required parts.
/// </summary>
public record WorkOrderGeneratorOutput(
    Guid ProposedWorkOrderId,
    string Title,
    string Description,
    decimal EstimatedCost,
    IReadOnlyList<string> RequiredParts);
