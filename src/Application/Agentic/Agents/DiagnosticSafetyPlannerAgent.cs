using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Options;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;
using Microsoft.Extensions.Options;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class DiagnosticSafetyPlannerAgent(
    ILLMProvider llmProvider,
    IToolDispatcher toolDispatcher,
    IOptions<AgenticOptions> options) : IAgent<DiagnosticPlanInput, DiagnosticPlanOutput>
{
    private readonly int _maxIterations = options.Value.MaxIterations;

    private static readonly IReadOnlyList<ToolDefinition> AllowedTools =
    [
        new(
            "GetEquipmentSpecs",
            "Retrieve the equipment specifications and operating limits needed for safe diagnosis.",
            ToolSchemas.GetEquipmentSpecsSchema),
        new(
            "GenerateSafetyChecklist",
            "Generate the mandatory safety checklist for the suspected equipment faults.",
            ToolSchemas.GenerateSafetyChecklistSchema)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string SystemPrompt = """
        You are an Industrial Safety & Diagnostic Planner.
        Create a diagnostic plan for the equipment using the matched symptoms and any tool results supplied.
        Extract every applicable safety prerequisite and mark each mandatory prerequisite with IsMandatory true.
        Safety prerequisites are critical to the FR-026 safety gate and must never be omitted or weakened.
        Use only the supplied context. Do not invent equipment details, faults, hazards, procedures, or evidence.
        Return strictly valid JSON only. Do not include Markdown, commentary, or additional properties.
        The JSON must match this DiagnosticPlanOutput contract exactly:
        {
          "Steps": [{ "Description": "string", "EvidenceChunkId": "string or null" }],
          "SafetyPrerequisites": [{ "Description": "string", "IsMandatory": true }],
          "Reasoning": "string"
        }
        """;

    public string Name => "DiagnosticSafetyPlanner";

    public async Task<AgentResult<DiagnosticPlanOutput>> ExecuteAsync(
        DiagnosticPlanInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var baseUserPrompt = $"""
            Plan diagnostics for the following maintenance request.

            Equipment identifier:
            {input.EquipmentId}

            Manual revision:
            {input.ManualRevision}

            Matched symptoms and faults:
            {JsonSerializer.Serialize(input.Symptoms, JsonOptions)}

            No tool results have been retrieved yet.
            """;

        int iteration = 0;
        string currentPrompt = baseUserPrompt;
        string currentSystemPrompt = SystemPrompt + "\nYou may use only the supplied GetEquipmentSpecs and GenerateSafetyChecklist tools.";
        string? finalText = null;

        // --- BOUNDED ITERATION LOOP (FR-024 / AG-008) ---
        while (iteration < _maxIterations)
        {
            iteration++;
            var completion = await AgentEventRecorder.CompleteAsync(
                llmProvider,
                new CompletionRequest(currentPrompt, currentSystemPrompt, Tools: AllowedTools),
                context,
                Name,
                iteration,
                cancellationToken);

            if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
            {
                finalText = completion.Text;
                break;
            }

            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Failure<DiagnosticPlanOutput>($"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
                }

                var dispatchResult = await AgentEventRecorder.DispatchAsync(
                    toolDispatcher,
                    new ToolInvocationRequest(toolCall.Name, toolCall.ArgumentsJson, CreateInvocationContext(context)),
                    context,
                    Name,
                    iteration,
                    toolCall.Id,
                    cancellationToken);

                if (!dispatchResult.Succeeded)
                {
                    return Failure<DiagnosticPlanOutput>(dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                currentPrompt += $"\n\nTool '{toolCall.Name}' result:\n{dispatchResult.ResultJson}";
            }
            
            currentSystemPrompt = SystemPrompt + "\nYou have retrieved tool results. You may use more tools if needed, or return the final JSON diagnostic plan if you have enough information.";
        }

        if (finalText is null)
        {
            // Iteration Breaker Triggered
            return Failure<DiagnosticPlanOutput>($"Agent '{Name}' exceeded maximum tool-calling iterations ({_maxIterations}) without producing a final output.");
        }

        // --- JSON SCHEMA RETRY LOOP ---
        const int MaxRetries = 2;
        string? lastError = null;
        DiagnosticPlanOutput? output = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            string textToParse;
            
            if (attempt == 0)
            {
                textToParse = finalText;
            }
            else
            {
                string promptForAttempt = $"{currentPrompt}\n\nYour previous response was invalid JSON and failed to parse with the following error:\n{lastError}\nPlease correct your response and return strictly valid JSON matching the schema. Do not include Markdown or commentary.";

                var finalCompletion = await AgentEventRecorder.CompleteAsync(
                    llmProvider,
                    new CompletionRequest(promptForAttempt, SystemPrompt),
                    context,
                    Name,
                    iteration + attempt,
                    cancellationToken);
                    
                textToParse = finalCompletion.Text;
            }

            try
            {
                output = JsonSerializer.Deserialize<DiagnosticPlanOutput>(textToParse, JsonOptions)
                    ?? throw new JsonException("The diagnostic planner returned an empty result.");
                break;
            }
            catch (JsonException exception)
            {
                lastError = exception.Message;
                if (attempt == MaxRetries)
                {
                    return Failure<DiagnosticPlanOutput>(
                        $"The diagnostic planner returned invalid structured output after {MaxRetries + 1} attempts: {lastError}");
                }
            }
        }

        return new AgentResult<DiagnosticPlanOutput>(
            output! with
            {
                Steps = output!.Steps ?? [],
                SafetyPrerequisites = output.SafetyPrerequisites ?? [],
                Reasoning = output.Reasoning ?? string.Empty
            },
            []);
    }

    private ToolInvocationContext CreateInvocationContext(IAgentContext context) =>
        new(
            ParseGuid(context.UserId),
            "Technician",
            ParseGuid(context.CorrelationId),
            Guid.NewGuid(),
            Name,
            context.CorrelationId);

    private static Guid ParseGuid(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;

    private static AgentResult<T> Failure<T>(string error) =>
        new(default!, [], false, error);
}