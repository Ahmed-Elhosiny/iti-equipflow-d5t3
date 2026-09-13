namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Provides an abstraction for synchronous and streaming LLM generation.
/// </summary>
public interface ILLMGenerationPort
{
    /// <summary>
    /// Generates a complete LLM response.
    /// </summary>
    /// <param name="request">The generation request.</param>
    /// <param name="cancellationToken">The token used to cancel generation.</param>
    /// <returns>The completed LLM result.</returns>
    Task<LLMResult> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams incremental LLM response chunks.
    /// </summary>
    /// <param name="request">The generation request.</param>
    /// <param name="cancellationToken">The token used to cancel generation.</param>
    /// <returns>An asynchronous sequence of response chunks.</returns>
    IAsyncEnumerable<LLMStreamChunk> StreamAsync(
        LLMRequest request,
        CancellationToken cancellationToken);
}
