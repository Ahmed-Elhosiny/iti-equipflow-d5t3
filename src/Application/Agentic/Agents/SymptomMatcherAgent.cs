using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
using EquipFlow.Application.Agentic.Events;
using EquipFlow.Application.Prompts;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Application.Agentic.Agents;

public sealed class SymptomMatcherAgent(
    ILLMProvider llmProvider,
    IToolDispatcher toolDispatcher) : IAgent<SymptomMatchInput, SymptomMatchOutput>
{
    private static readonly IReadOnlyList<ToolDefinition> AllowedTools =
    [
        new(
            "SearchManuals",
            "Search equipment manuals for relevant maintenance evidence.",
            ToolSchemas.SearchManualsSchema),
        new(
            "QueryFaultHistory",
            "Find historical faults for the specified equipment and symptom.",
            ToolSchemas.QueryFaultHistorySchema)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "SymptomMatcher";

       public async Task<AgentResult<SymptomMatchOutput>> ExecuteAsync(
        SymptomMatchInput input,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var prompt = AgentPromptTemplates.SymptomMatcherPrompt;
        var baseUserPrompt = prompt.UserPromptTemplate
            .Replace("{SymptomDescription}", input.SymptomDescription, StringComparison.Ordinal)
            .Replace("{EquipmentId}", input.EquipmentIdHint ?? "unknown", StringComparison.Ordinal)
            .Replace("{Evidence}", "No evidence retrieved yet.", StringComparison.Ordinal);

        var systemPromptWithTools = $"{prompt.SystemPrompt}\nYou may use the supplied SearchManuals and QueryFaultHistory tools when evidence is needed.";

        var completion = await AgentEventRecorder.CompleteAsync(
            llmProvider,
            new CompletionRequest(baseUserPrompt, systemPromptWithTools, Tools: AllowedTools),
            context,
            Name,
            1,
            cancellationToken);

        var evidence = new List<EvidenceChunk>();
        string finalUserPrompt = baseUserPrompt;
        string? initialText = null;

        if (completion.ToolCalls is not null)
        {
            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Failure<SymptomMatchOutput>($"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
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
                    return Failure<SymptomMatchOutput>(dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                try
                {
                    evidence.AddRange(ReadEvidence(dispatchResult.ResultJson));
                }
                catch (JsonException exception)
                {
                    return Failure<SymptomMatchOutput>($"Tool '{toolCall.Name}' returned invalid evidence: {exception.Message}");
                }
            }

            finalUserPrompt = $"{baseUserPrompt}\n\nRetrieved evidence:\n{JsonSerializer.Serialize(evidence, JsonOptions)}";
        }
        else
        {
            initialText = completion.Text;
        }

        const int MaxRetries = 2;
        string? lastError = null;
        SymptomMatchOutput? output = null;

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
                    new CompletionRequest(promptForAttempt, prompt.SystemPrompt),
                    context,
                    Name,
                    completion.ToolCalls is not null ? 2 : 1 + attempt,
                    cancellationToken);
                    
                textToParse = finalCompletion.Text;
            }

            try
            {
                output = JsonSerializer.Deserialize<SymptomMatchOutput>(textToParse, JsonOptions)
                    ?? throw new JsonException("The symptom matcher returned an empty result.");
                break;
            }
            catch (JsonException exception)
            {
                lastError = exception.Message;
                if (attempt == MaxRetries)
                {
                    return Failure<SymptomMatchOutput>(
                        $"The symptom matcher returned invalid structured output after {MaxRetries + 1} attempts: {lastError}");
                }
            }
        }

        output = output! with
        {
            EquipmentId = string.IsNullOrWhiteSpace(output.EquipmentId)
                ? input.EquipmentIdHint ?? string.Empty
                : output.EquipmentId,
            ManualRevision = string.IsNullOrWhiteSpace(output.ManualRevision)
                ? "unknown"
                : output.ManualRevision,
            MatchedSymptoms = output.MatchedSymptoms.Count == 0
                ? [input.SymptomDescription]
                : output.MatchedSymptoms
        };

        var citations = evidence
            .Select(chunk => new Citation(
                chunk.ChunkId.ToString(),
                chunk.ChunkId.ToString(),
                null,
                null,
                (float)chunk.Score))
            .ToArray();

        return new AgentResult<SymptomMatchOutput>(output with { EvidenceChunks = evidence }, citations);
    }
    private ToolInvocationContext CreateInvocationContext(IAgentContext context) =>
        new(
            ParseGuid(context.UserId),
            "Technician",
            ParseGuid(context.CorrelationId),
            Guid.NewGuid(),
            Name,
            context.CorrelationId);

    private static IEnumerable<EvidenceChunk> ReadEvidence(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            yield break;
        }

        using var document = JsonDocument.Parse(resultJson);
        if (!document.RootElement.TryGetProperty("chunks", out var chunks))
        {
            yield break;
        }

        foreach (var chunk in chunks.EnumerateArray())
        {
            yield return new EvidenceChunk(
                chunk.GetProperty("chunkId").GetGuid(),
                chunk.GetProperty("content").GetString() ?? string.Empty,
                chunk.GetProperty("score").GetDouble());
        }
    }

    private static Guid ParseGuid(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;

    private static AgentResult<T> Failure<T>(string error) =>
        new(default!, [], false, error);
}