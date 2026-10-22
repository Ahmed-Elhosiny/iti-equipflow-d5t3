using System.Runtime.CompilerServices;
using EquipFlow.Application.Ports.LLM;
using EquipFlow.Domain.Budget.ValueObjects;

namespace EquipFlow.Infrastructure.LLM;

/// <summary>
/// Provides deterministic LLM generation for tests.
/// </summary>
public sealed class MockLLMGenerationAdapter : ILLMGenerationPort
{
    private const string CompletionContent = "Mock LLM completion for EquipFlow tests.";

    /// <inheritdoc />
        public Task<LLMResult> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            throw new InvalidOperationException("ADR-004 Violation: LLM generation requires a valid Budget ReservationId.");
        }

        var promptText = string.Join(" ", request.Messages.Select(m => m.Content ?? string.Empty));
        string completionContent;

        // Deterministic JSON routing based on the agent calling the LLM
        if (promptText.Contains("SymptomMatcher", StringComparison.OrdinalIgnoreCase) || request.Tools?.Any(t => t.Name == "SearchManuals") == true)
        {
            // FIX: Added dummy EvidenceChunks with Score > 0.4 to pass the AG-007 groundedness check
            completionContent = """
            {
              "EquipmentId": "P-101",
              "ManualRevision": "2.1",
              "MatchedSymptoms": ["overheating", "grinding noise"],
              "Reasoning": "Mock Symptom Matcher reasoning.",
              "IsGrounded": true,
              "RankedFaults": [{"FaultName": "Bearing Failure", "ConfidenceScore": 0.9, "EvidenceChunkIds": []}],
              "EvidenceChunks": [
                {
                  "ChunkId": "11111111-1111-1111-1111-111111111111",
                  "Content": "Mock evidence: P-101 overheating due to bearing failure.",
                  "Score": 0.95,
                  "DocumentId": "22222222-2222-2222-2222-222222222222",
                  "DocumentTitle": "P-101 Mock Manual",
                  "Page": 10,
                  "Section": "Troubleshooting"
                }
              ]
            }
            """;
        }
        else if (promptText.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase) || request.Tools?.Any(t => t.Name == "GenerateSafetyChecklist") == true)
        {
            completionContent = """
            {
              "Steps": [{"Description": "Inspect primary components for abnormal wear.", "EvidenceChunkId": "11111111-1111-1111-1111-111111111111"}],
              "SafetyPrerequisites": [{"Description": "Perform Lockout/Tagout procedure.", "IsMandatory": true}],
              "Reasoning": "Mock Diagnostic Planner reasoning."
            }
            """;
        }
        else
        {
            completionContent = """
            {
              "Title": "Maintenance Work Order for P-101",
              "Description": "Address overheating and grinding noise by inspecting and replacing bearings.",
              "RequiredParts": ["Bearings", "Lubricant"],
              "Priority": "High",
              "EstimatedCost": 250.00,
              "Summary": "Mock Work Order Generator summary."
            }
            """;
        }

        var promptLength = request.Messages.Sum(m => m.Content?.Length ?? 0);
        var promptTokens = EstimateTokens(promptLength);
        var completionTokens = EstimateTokens(completionContent.Length);

        var result = new LLMResult(
            completionContent,
            null,
            "stop",
            promptTokens,
            completionTokens,
            TokenUsage.FromActual(promptTokens, completionTokens),
            "Mock",
            "mock-model",
            0m);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            throw new InvalidOperationException("ADR-004 Violation: LLM generation requires a valid Budget ReservationId.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        yield return new LLMStreamChunk("Mock ", null, false, null);

        cancellationToken.ThrowIfCancellationRequested();
        yield return new LLMStreamChunk("LLM ", null, false, null);

        cancellationToken.ThrowIfCancellationRequested();
        yield return new LLMStreamChunk("stream.", null, false, null);

        cancellationToken.ThrowIfCancellationRequested();
        yield return new LLMStreamChunk(null, null, true, "stop");
    }

    private static int EstimateTokens(int length) => Math.Max(1, length / 4);
}