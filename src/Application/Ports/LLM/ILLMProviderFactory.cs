namespace EquipFlow.Application.Ports.LLM;

/// <summary>
/// Resolves specific LLM generation ports by their registered provider key,
/// such as "OpenAI", "Ollama", or "Mock", to support budget-aware routing
/// and fallback cascades.
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// Gets the LLM generation port registered for the specified provider key.
    /// </summary>
    /// <param name="providerName">The registered provider key.</param>
    /// <returns>The matching LLM generation port.</returns>
    ILLMGenerationPort GetProvider(string providerName);
}
