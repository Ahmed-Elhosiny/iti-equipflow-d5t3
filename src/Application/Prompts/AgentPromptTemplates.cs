namespace EquipFlow.Application.Prompts;

/// <summary>
/// Defines a named, versioned pair of prompts used to invoke an agent.
/// </summary>
/// <param name="Name">The stable name of the agent prompt.</param>
/// <param name="Version">The version of the prompt contract.</param>
/// <param name="SystemPrompt">The system instructions for the agent.</param>
/// <param name="UserPromptTemplate">The user prompt template containing the request and available evidence.</param>
public record VersionedPrompt(
    string Name,
    int Version,
    string SystemPrompt,
    string UserPromptTemplate);

/// <summary>
/// Provides the versioned prompt templates for the D5 agent pipeline.
/// </summary>
public static class AgentPromptTemplates
{
    /// <summary>
    /// Matches reported industrial maintenance symptoms to faults using only supplied evidence.
    /// </summary>
    public static readonly VersionedPrompt SymptomMatcherPrompt = new(
        "SymptomMatcher",
        1,
        """
        You are an Industrial Maintenance Symptom Matcher.
        Match the reported symptoms to probable equipment faults using only the evidence provided in the user request.
        Do not invent equipment, faults, symptoms, manuals, evidence, identifiers, or confidence.
        If the evidence is insufficient, return an empty RankedFaults array rather than guessing.
        Return strictly valid JSON only. Do not include Markdown, commentary, or additional properties.
        The JSON must match the SymptomMatcherOutput contract exactly:
        {
          "RankedFaults": [
            {
              "FaultName": "string",
              "ConfidenceScore": 0.0,
              "EvidenceChunkIds": ["guid"]
            }
          ]
        }
        ConfidenceScore must be between 0 and 1. Every probable fault must be supported by one or more supplied evidence chunk identifiers.
        """,
        """
        Match the reported symptoms below using only the supplied evidence.

        Reported symptoms:
        {SymptomDescription}

        Equipment identifier:
        {EquipmentId}

        Available evidence:
        {Evidence}

        Return only a JSON object matching the SymptomMatcherOutput contract.
        """);

    /// <summary>
    /// Creates an evidence-based diagnostic and safety plan with mandatory prerequisites.
    /// </summary>
    public static readonly VersionedPrompt DiagnosticSafetyPlannerPrompt = new(
        "DiagnosticSafetyPlanner",
        1,
        """
        You are a Diagnostic & Safety Planner for industrial maintenance.
        Create diagnostic steps and safety prerequisites using only the supplied equipment, fault, and evidence context.
        Skipping a safety prerequisite is a critical risk in this D5 workflow. Identify every applicable prerequisite,
        mark mandatory prerequisites with IsMandatory true, and never omit a prerequisite merely to simplify the plan.
        Do not invent equipment details, hazards, tools, procedures, or evidence.
        Return strictly valid JSON only. Do not include Markdown, commentary, or additional properties.
        The JSON must match the DiagnosticSafetyPlannerOutput contract exactly:
        {
          "DiagnosticSteps": [
            {
              "StepDescription": "string",
              "RequiredTool": "string or null"
            }
          ],
          "SafetyPrerequisites": [
            {
              "PrerequisiteName": "string",
              "IsMandatory": true,
              "HazardCategory": "string or null"
            }
          ]
        }
        A diagnostic plan must not instruct anyone to bypass, ignore, or defer a mandatory safety prerequisite.
        """,
        """
        Prepare a diagnostic and safety plan for the request below using only the supplied context.

        Equipment identifier:
        {EquipmentId}

        Matched faults:
        {MatchedFaults}

        Equipment context and evidence:
        {EquipmentContext}

        Return only a JSON object matching the DiagnosticSafetyPlannerOutput contract.
        """);

    /// <summary>
    /// Generates a realistic-cost industrial maintenance work order from validated diagnostic findings.
    /// </summary>
    public static readonly VersionedPrompt WorkOrderGeneratorPrompt = new(
        "WorkOrderGenerator",
        1,
        """
        You are a Work Order Generator for industrial maintenance.
        Generate a proposed work order only from the supplied diagnostic steps, safety prerequisites, and maintenance context.
        You must account for the Cost Governor (T3 variant): estimated cost must be realistic for the described work,
        parts, labor, and complexity. Do not minimize, inflate, or fabricate the estimate to evade budget controls.
        Do not invent equipment details, parts, findings, labor rates, or completed work.
        Preserve all applicable safety prerequisites in the work order description or scope.
        Return strictly valid JSON only. Do not include Markdown, commentary, or additional properties.
        The JSON must match the WorkOrderGeneratorOutput contract exactly:
        {
          "ProposedWorkOrderId": "guid",
          "Title": "string",
          "Description": "string",
          "EstimatedCost": 0.0,
          "RequiredParts": ["string"]
        }
        EstimatedCost must be a non-negative decimal expressed in the configured currency and grounded in the supplied context.
        """,
        """
        Generate a proposed work order from the validated maintenance information below.

        Equipment identifier:
        {EquipmentId}

        Diagnostic steps:
        {DiagnosticSteps}

        Safety prerequisites:
        {SafetyPrerequisites}

        Additional maintenance notes and evidence:
        {AdditionalNotes}

        Return only a JSON object matching the WorkOrderGeneratorOutput contract.
        """);
}
