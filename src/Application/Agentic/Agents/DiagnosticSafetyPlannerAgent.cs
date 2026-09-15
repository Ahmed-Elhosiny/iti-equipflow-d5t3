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
        if (completion.ToolCalls is not null)
        {
            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Failure<DiagnosticPlanOutput>(
                        $"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
                }

                var dispatchResult = await AgentEventRecorder.DispatchAsync(
                    toolDispatcher,
                    new ToolInvocationRequest(
                        toolCall.Name,
                        toolCall.ArgumentsJson,
                        CreateInvocationContext(context)),
                    context,
                    Name,
                    1,
                    toolCall.Id,
                    cancellationToken);

                if (!dispatchResult.Succeeded)
                {
                    return Failure<DiagnosticPlanOutput>(
                        dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                toolResults.Add(JsonSerializer.Serialize(
                    new { Tool = toolCall.Name, Result = dispatchResult.ResultJson },
                    JsonOptions));
            }

            completion = await AgentEventRecorder.CompleteAsync(
                llmProvider,
                new CompletionRequest(
                    $"{userPrompt}\n\nTool results:\n{string.Join("\n", toolResults)}\n\nReturn the final diagnostic plan now.",
                    SystemPrompt),
                context,
                Name,
                2,
                cancellationToken);
        }

        try
        {
            var output = JsonSerializer.Deserialize<DiagnosticPlanOutput>(completion.Text, JsonOptions)
                ?? throw new JsonException("The diagnostic planner returned an empty result.");

            return new AgentResult<DiagnosticPlanOutput>(
                output with
                {
                    Steps = output.Steps ?? [],
                    SafetyPrerequisites = output.SafetyPrerequisites ?? [],
                    Reasoning = output.Reasoning ?? string.Empty
                },
                []);
        }
        catch (JsonException exception)
        {
            return Failure<DiagnosticPlanOutput>(
                $"The diagnostic planner returned invalid structured output: {exception.Message}");
        }
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