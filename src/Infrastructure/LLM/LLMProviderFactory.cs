using EquipFlow.Application.Ports.LLM;
using Microsoft.Extensions.DependencyInjection;

namespace EquipFlow.Infrastructure.LLM;

/// <summary>
/// Resolves registered LLM generation adapters by provider key.
/// </summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMProviderFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve keyed adapters.</param>
    public LLMProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets the LLM generation adapter registered for the specified provider key.
    /// </summary>
    /// <param name="providerName">The registered provider key.</param>
    /// <returns>The matching LLM generation adapter.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerName"/> is null or whitespace.</exception>
    public ILLMGenerationPort GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
        }

        return _serviceProvider.GetRequiredKeyedService<ILLMGenerationPort>(providerName);
    }
}