namespace EquipFlow.Application.Agents.Contracts;

/// <summary>
/// Describes the symptoms to be matched against known equipment faults.
/// </summary>
public record SymptomMatcherInput(
    Guid EquipmentId,
    string SymptomDescription,
    string? AdditionalContext);

/// <summary>
/// Contains faults ranked by their likelihood of explaining the reported symptoms.
/// </summary>
public record SymptomMatcherOutput(
    IReadOnlyList<SymptomMatcherOutput.ProbableFault> RankedFaults)
{
    /// <summary>
    /// Describes a probable fault and the evidence supporting the match.
    /// </summary>
    public record ProbableFault(
        string FaultName,
        double ConfidenceScore,
        IReadOnlyList<Guid> EvidenceChunkIds);
}