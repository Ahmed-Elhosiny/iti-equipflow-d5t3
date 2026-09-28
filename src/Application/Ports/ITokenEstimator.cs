namespace EquipFlow.Application.Ports;

/// <summary>
/// Estimates the token count for a given text or collection of texts.
/// Used for pre-flight cost estimation (T3 Cost Governor).
/// </summary>
public interface ITokenEstimator
{
    /// <summary>
    /// Estimates the number of tokens in the provided text.
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(string? text);

    /// <summary>
    /// Estimates the number of tokens across multiple text segments.
    /// </summary>
    /// <param name="texts">The collection of texts to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(IEnumerable<string?> texts);
}