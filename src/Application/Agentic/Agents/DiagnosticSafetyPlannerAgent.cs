using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class DiagnosticSafetyPlannerAgent(
    ILLMProvider llmProvider,
    IToolDispatcher toolDispatcher) : IAgent<DiagnosticPlanInput, DiagnosticPlanOutput>
{
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

        var userPrompt = $"""
            Plan diagnostics for the following maintenance request.

            Equipment identifier:
            {input.EquipmentId}

            Manual revision:
            {input.ManualRevision}

            Matched symptoms and faults:
            {JsonSerializer.Serialize(input.Symptoms, JsonOptions)}

            No tool results have been retrieved yet.
            """;

        var completion = await AgentEventRecorder.CompleteAsync(
            llmProvider,
            new CompletionRequest(
                userPrompt,
                SystemPrompt + "\nYou may use only the supplied GetEquipmentSpecs and GenerateSafetyChecklist tools.",
                Tools: AllowedTools),
            context,
            Name,
            1,
            cancellationToken);

        var toolResults = new List<string>();
        string finalUserPrompt = userPrompt;
        string? initialText = null;

        if (completion.ToolCalls is not null)
        {
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
                    1,
                    toolCall.Id,
                    cancellationToken);

                if (!dispatchResult.Succeeded)
                {
                    return Failure<DiagnosticPlanOutput>(dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                toolResults.Add(JsonSerializer.Serialize(
                    new { Tool = toolCall.Name, Result = dispatchResult.ResultJson },
                    JsonOptions));
            }

            finalUserPrompt = $"{userPrompt}\n\nTool results:\n{string.Join("\n", toolResults)}\n\nReturn the final diagnostic plan now.";
        }
        else
        {
            initialText = completion.Text;
        }

        const int MaxRetries = 2;
        string? lastError = null;
        DiagnosticPlanOutput? output = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            string textToParse;
            
            if (attempt == 0 && initialText is not null)
            {
                textToParse = initialText;
            }
            else
            {
                string promptForAttempt = finalUserPrompt;
                if (lastError is not null)
                {
                    promptForAttempt += $"\n\nYour previous response was invalid JSON and failed to parse with the following error:\n{lastError}\nPlease correct your response and return strictly valid JSON matching the schema. Do not include Markdown or commentary.";
                }

                var finalCompletion = await AgentEventRecorder.CompleteAsync(
                    llmProvider,
                    new CompletionRequest(promptForAttempt, SystemPrompt),
                    context,
                    Name,
                    completion.ToolCalls is not null ? 2 : 1 + attempt,
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