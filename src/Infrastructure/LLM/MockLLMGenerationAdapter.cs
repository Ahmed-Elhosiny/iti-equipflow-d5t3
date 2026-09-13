using System.Runtime.CompilerServices;
using EquipFlow.Application.Ports.LLM;

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

        var promptLength = 0;
        foreach (var message in request.Messages)
        {
            promptLength += message.Content.Length;
        }

        var result = new LLMResult(
            CompletionContent,
            null,
            "stop",
            EstimateTokens(promptLength),
            EstimateTokens(CompletionContent.Length));

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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