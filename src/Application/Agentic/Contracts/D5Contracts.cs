using System.Collections.Generic;

namespace EquipFlow.Application.Agentic.Contracts;

// Agent 1: Symptom Matcher
public record SymptomMatchInput(string SymptomDescription, string? EquipmentIdHint = null);
public record SymptomMatchOutput(string EquipmentId, string ManualRevision, IReadOnlyList<string> MatchedSymptoms, string Reasoning);

// Agent 2: Diagnostic & Safety Planner
public record DiagnosticPlanInput(string EquipmentId, string ManualRevision, IReadOnlyList<string> Symptoms);
public record DiagnosticPlanOutput(IReadOnlyList<DiagnosticStep> Steps, IReadOnlyList<SafetyPrerequisite> SafetyPrerequisites, string Reasoning);
public record DiagnosticStep(string Description, string? EvidenceChunkId);
public record SafetyPrerequisite(string Description, bool IsMandatory);

// Agent 3: Work Order Generator
public record WorkOrderInput(string EquipmentId, DiagnosticPlanOutput DiagnosticPlan);
public record WorkOrderOutput(string Title, string Description, IReadOnlyList<string> RequiredParts, string Priority);
