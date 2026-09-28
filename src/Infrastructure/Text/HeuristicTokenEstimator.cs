using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Text;

/// <summary>
/// Provides a heuristic token estimation based on character count.
/// Approximates 1 token per 4 characters, plus a base overhead for system prompts and tool schemas.
/// </summary>
public sealed class HeuristicTokenEstimator : ITokenEstimator
{
    private const int CharactersPerToken = 4;
    
    // Base overhead accounts for the 3 agent system prompts, tool definitions, and JSON schema boilerplate
    // that are not explicitly part of the user's symptom description but are sent to the LLM.
    private const int BaseWorkflowOverheadTokens = 1500; 

    /// <inheritdoc />
    public int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return BaseWorkflowOverheadTokens;
        return (text.Length / CharactersPerToken) + BaseWorkflowOverheadTokens;
    }

    /// <inheritdoc />
    public int EstimateTokens(IEnumerable<string?> texts)
    {
        if (texts is null) return BaseWorkflowOverheadTokens;
        var totalChars = texts.Sum(t => t?.Length ?? 0);
        return (totalChars / CharactersPerToken) + BaseWorkflowOverheadTokens;
    }
}