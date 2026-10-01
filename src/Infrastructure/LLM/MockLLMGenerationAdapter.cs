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


        var promptLength = 0;
        foreach (var message in request.Messages)
        {
            promptLength += message.Content.Length;
        }

                var promptTokens = EstimateTokens(promptLength);
        var completionTokens = EstimateTokens(CompletionContent.Length);

        var result = new LLMResult(
            CompletionContent,
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