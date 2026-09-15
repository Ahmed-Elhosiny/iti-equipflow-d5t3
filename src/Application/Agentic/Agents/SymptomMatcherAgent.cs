using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Agentic.Contracts;
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
        var userPrompt = prompt.UserPromptTemplate
            .Replace("{SymptomDescription}", input.SymptomDescription, StringComparison.Ordinal)
            .Replace("{EquipmentId}", input.EquipmentIdHint ?? "unknown", StringComparison.Ordinal)
            .Replace("{Evidence}", "No evidence retrieved yet.", StringComparison.Ordinal);

        var completion = await llmProvider.CompleteAsync(
            new CompletionRequest(
                userPrompt,
                $"{prompt.SystemPrompt}\nYou may use the supplied SearchManuals and QueryFaultHistory tools when evidence is needed.",
                Tools: AllowedTools),
            cancellationToken);

        var evidence = new List<EvidenceChunk>();
        if (completion.ToolCalls is not null)
        {
            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AllowedTools.Any(tool => string.Equals(tool.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Failure<SymptomMatchOutput>(
                        $"Tool '{toolCall.Name}' is not allowed for agent '{Name}'.");
                }

                var dispatchResult = await toolDispatcher.DispatchAsync(
                    new ToolInvocationRequest(
                        toolCall.Name,
                        toolCall.ArgumentsJson,
                        CreateInvocationContext(context)),
                    cancellationToken);

                if (!dispatchResult.Succeeded)
                {
                    return Failure<SymptomMatchOutput>(
                        dispatchResult.ErrorMessage ?? $"Tool '{toolCall.Name}' failed.");
                }

                try
                {
                    evidence.AddRange(ReadEvidence(dispatchResult.ResultJson));
                }
                catch (JsonException exception)
                {
                    return Failure<SymptomMatchOutput>(
                        $"Tool '{toolCall.Name}' returned invalid evidence: {exception.Message}");
                }
            }

            completion = await llmProvider.CompleteAsync(
                new CompletionRequest(
                    $"{userPrompt}\n\nRetrieved evidence:\n{JsonSerializer.Serialize(evidence, JsonOptions)}",
                    prompt.SystemPrompt),
                cancellationToken);
        }

        SymptomMatchOutput output;
        try
        {
            output = JsonSerializer.Deserialize<SymptomMatchOutput>(completion.Text, JsonOptions)
                ?? throw new JsonException("The symptom matcher returned an empty result.");
        }
        catch (JsonException exception)
        {
            return Failure<SymptomMatchOutput>(
                $"The symptom matcher returned invalid structured output: {exception.Message}");
        }

        output = output with
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